using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Equipment;
using Forge.Game.Events;
using Forge.Game.Items;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Game
{
    public class WeaponReadinessTests
    {
        static readonly RecordId Ring = EquipSlotRecord.IdFor("forge.core", "ring");

        static readonly BaseObjectRecord Sword = Item("sword", new EquippablePart(CoreEquipSlots.OneHanded()));
        static readonly BaseObjectRecord Band = Item("band", new EquippablePart(new[] { EquipFit.Of(Ring) }));

        static readonly CombatStyleRecord Archery = new CombatStyleRecord(CombatStyleRecord.IdFor("forge.core", "archery"), "Archery", new[]
        {
            new CombatMove(CombatMoveKind.Draw, "BowDraw"),
            new CombatMove(CombatMoveKind.Sheathe, "BowSheathe"),
        });

        static readonly BaseObjectRecord Bow = new BaseObjectRecord(new RecordId("forge.core", "bow"), "bow", "items/bow",
            item: new ItemPart(ItemKind.Weapon, 1f, 1), equippable: new EquippablePart(CoreEquipSlots.Bow()),
            weapon: new WeaponPart(Archery.Id, new[] { Effect.Damage(CoreAttributes.Health, 7f) }));

        // The look's timings: each named move lasts `Seconds`, the weapon changing place a quarter of the way in.
        sealed class Clips : IReadyTimings
        {
            public float Seconds = 2f;

            public bool TryGet(string animation, out ReadyTiming timing)
            {
                timing = new ReadyTiming(Seconds, 0.25f);
                return animation == "BowDraw" || animation == "BowSheathe";
            }
        }

        Inventory _inventory;
        GameEvents _events;
        ActorEquipment _equipment;
        WeaponReadiness _readiness;
        List<IGameEvent> _told;

        [SetUp]
        public void SetUp()
        {
            var world = LoadOrderResolver.Resolve(new[]
            {
                Plugin("forge.core",
                    new EquipSlotRecord(CoreEquipSlots.RightHand, "Right Hand", "RightHand", sheathes: true),
                    new EquipSlotRecord(CoreEquipSlots.LeftHand, "Left Hand", "LeftHand", sheathes: true),
                    new EquipSlotRecord(Ring, "Ring"),
                    Sword,
                    Band,
                    Archery,
                    Bow),
            }).Records;
            _inventory = new Inventory();
            _events = new GameEvents();
            _equipment = new ActorEquipment(ActorId.Player, _inventory, _events);
            _readiness = new WeaponReadiness(_equipment, world, _events);
            _readiness.SetTimes(0.8f, 0.6f);
            _told = new List<IGameEvent>();
            _events.SubscribeAll(_told.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _readiness.Dispose();
            _equipment.Dispose();
        }

        [Test]
        public void ASword_IsDrawnOverItsDrawTime_AndSheathedOverItsOwn()
        {
            Wield(Sword);
            Assert.That(_readiness.State, Is.EqualTo(Readiness.Sheathed), "Equipping never draws.");

            Assert.That(_readiness.Toggle(), Is.True);
            _readiness.Tick(0.4f);
            Assert.That((_readiness.State, _readiness.Progress), Is.EqualTo((Readiness.Drawing, 0.5f)));

            _readiness.Tick(0.4f);
            _events.Flush();
            Assert.That(_readiness.State, Is.EqualTo(Readiness.Drawn));
            Assert.That(_told.OfType<WeaponsDrawn>().Single().Actor, Is.EqualTo(ActorId.Player));

            _readiness.Toggle();
            _readiness.Tick(0.6f);
            _events.Flush();
            Assert.That(_readiness.State, Is.EqualTo(Readiness.Sheathed));
            Assert.That(_told.OfType<WeaponsSheathed>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void ADrawOrASheathe_RunsToItsEnd_WhateverIsPressedMeanwhile()
        {
            Wield(Sword);
            _readiness.Toggle();

            Assert.That(_readiness.Toggle(), Is.False);
            Assert.That(_readiness.State, Is.EqualTo(Readiness.Drawing));
        }

        [Test]
        public void WithNothingToDraw_ItStaysSheathed_AndARingIsNothingToDraw()
        {
            Assert.That(_readiness.Toggle(), Is.False);

            Wield(Band);

            Assert.That(_readiness.HasWeapons, Is.False);
            Assert.That(_readiness.Toggle(), Is.False);
            Assert.That(_readiness.State, Is.EqualTo(Readiness.Sheathed));
        }

        [Test]
        public void TakingOffTheLastWeapon_SheathesAtOnce()
        {
            Wield(Sword);
            _readiness.Toggle();
            _readiness.Tick(1f);
            var changes = new List<Readiness>();
            _readiness.Changed += changes.Add;

            _inventory.TryRemove(Sword.Id);
            _events.Flush();

            Assert.That(_readiness.State, Is.EqualTo(Readiness.Sheathed));
            Assert.That(changes, Is.EqualTo(new[] { Readiness.Sheathed }));
            Assert.That(_told.OfType<WeaponsSheathed>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheWeapon_ReachesTheHandPartWayIntoTheDraw_AndTheSheathPartWayIntoTheSheathe()
        {
            Wield(Sword);
            _readiness.SetTimes(1f, 1f, drawnAt: 0.4f, sheathedAt: 0.7f);
            var told = new List<bool>();
            _readiness.InHandChanged += told.Add;

            _readiness.Toggle();
            _readiness.Tick(0.3f);
            Assert.That(_readiness.InHand, Is.False, "Still reaching for the hilt.");
            _readiness.Tick(0.2f);
            Assert.That(_readiness.InHand, Is.True, "Out of the sheath.");
            _readiness.Tick(1f);

            _readiness.Toggle();
            _readiness.Tick(0.6f);
            Assert.That(_readiness.InHand, Is.True, "On its way back.");
            _readiness.Tick(0.2f);
            Assert.That(_readiness.InHand, Is.False, "In the sheath before the arm is back.");
            _readiness.Tick(1f);

            Assert.That(told, Is.EqualTo(new[] { true, false }), "Told once each way.");
        }

        [Test]
        public void ADrawOfNoTime_IsDoneAtOnce()
        {
            Wield(Sword);
            _readiness.SetTimes(0f, 0f);

            _readiness.Toggle();

            Assert.That(_readiness.State, Is.EqualTo(Readiness.Drawn));
        }

        [Test]
        public void ABow_IsDrawnAndPutAwayByItsStylesMoves_TimedAsTheLookPlaysThem()
        {
            Wield(Bow);
            _readiness.Timings = new Clips();
            Assert.That((_readiness.DrawAnimation, _readiness.SheatheAnimation), Is.EqualTo(("BowDraw", "BowSheathe")));

            _readiness.Toggle();
            _readiness.Tick(0.4f);
            Assert.That(_readiness.InHand, Is.False, "Before a quarter of its 2 s it is still on the back...");
            _readiness.Tick(0.2f);
            Assert.That(_readiness.InHand, Is.True, "...then in the hand.");
            _readiness.Tick(1.4f);
            Assert.That(_readiness.State, Is.EqualTo(Readiness.Drawn), "Its draw lasts its own 2 s, not the look's 0.8 s.");
        }

        [Test]
        public void AWeaponWhoseStyleNamesNoDraw_IsDrawnAsTheLookDrawsOwn()
        {
            Wield(Sword);
            _readiness.Timings = new Clips();

            Assert.That(_readiness.DrawAnimation, Is.Null);
            Assert.That((_readiness.DrawSeconds, _readiness.SheatheSeconds), Is.EqualTo((0.8f, 0.6f)));
        }

        void Wield(BaseObjectRecord item)
        {
            _inventory.Add(item.Id);
            Assert.That(_equipment.TryEquip(item), Is.EqualTo(EquipOutcome.Equipped));
        }

        static BaseObjectRecord Item(string name, EquippablePart equippable) =>
            new BaseObjectRecord(new RecordId("forge.core", name), name, "items/" + name, item: new ItemPart(ItemKind.Weapon, 1f, 1), equippable: equippable);
    }
}
