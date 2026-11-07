using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.Game.Events;
using Forge.Game.Items;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Game
{
    public class CombatTests
    {
        static readonly RecordId OneHanded = CombatStyleRecord.IdFor("forge.core", "one_handed");
        static readonly RecordId Greatsword = CombatStyleRecord.IdFor("forge.core", "two_handed");
        static readonly ActorId Dummy = new ActorId("dummy");

        static readonly BaseObjectRecord Sword = Weapon("sword", CoreEquipSlots.OneHanded(), OneHanded, 12f);
        static readonly BaseObjectRecord Claymore = Weapon("claymore", CoreEquipSlots.TwoHanded(), Greatsword, 20f);

        IRecordStore _world;
        Inventory _inventory;
        GameEvents _events;
        ActorEquipment _equipment;
        WeaponReadiness _readiness;
        ActorAttributes _attacker;
        ActorAttributes _dummy;
        Combatants _combatants;
        ActorCombat _combat;
        List<IGameEvent> _told;

        static BaseObjectRecord Weapon(string name, EquipFit[] fits, RecordId style, float damage) =>
            new BaseObjectRecord(new RecordId("forge.core", name), name, "items/" + name, item: new ItemPart(ItemKind.Weapon, 5f, 10),
                equippable: new EquippablePart(fits), weapon: new WeaponPart(style, new[] { Effect.Damage(CoreAttributes.Health, damage) }));

        [SetUp]
        public void SetUp()
        {
            _world = LoadOrderResolver.Resolve(new[]
            {
                Plugin("forge.core",
                    new EquipSlotRecord(CoreEquipSlots.RightHand, "Right Hand", "RightHand", sheathes: true),
                    new EquipSlotRecord(CoreEquipSlots.LeftHand, "Left Hand", "LeftHand", sheathes: true),
                    new AttributeRecord(CoreAttributes.Health, "Health", AttributeKind.Resource, 30f),
                    new AttributeRecord(CoreAttributes.Stamina, "Stamina", AttributeKind.Resource, 10f),
                    new CombatStyleRecord(OneHanded, "One-Handed", new[] { new CombatMove(CombatMoveKind.Attack, "SwordSlash") }),
                    new CombatStyleRecord(Greatsword, "Two-Handed", new[] { new CombatMove(CombatMoveKind.Attack, "Cleave", staminaCost: 8f) }),
                    Sword,
                    Claymore),
            }).Records;
            var set = AttributeSet.From(_world.OfType<AttributeRecord>());
            _inventory = new Inventory();
            _events = new GameEvents();
            _equipment = new ActorEquipment(ActorId.Player, _inventory, _events);
            _readiness = new WeaponReadiness(_equipment, _world, _events);
            _readiness.SetTimes(0f, 0f);
            _attacker = new ActorAttributes(set, ActorId.Player);
            _dummy = new ActorAttributes(set, Dummy);
            _combatants = new Combatants();
            _combatants.Add(ActorId.Player, _attacker);
            _combatants.Add(Dummy, _dummy);
            _combat = new ActorCombat(_equipment, _readiness, _world, _attacker, _combatants, _events)
            {
                Timings = new FixedTimings(new MoveTiming(1f, 0.4f, 0.6f)),
            };
            _told = new List<IGameEvent>();
            _events.SubscribeAll(_told.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _combat.Dispose();
            _readiness.Dispose();
            _equipment.Dispose();
        }

        void Hold(BaseObjectRecord weapon, RecordId? slot = null)
        {
            _inventory.Add(weapon.Id);
            _equipment.TryEquip(weapon, slot);
        }

        void Draw()
        {
            _readiness.Toggle();
            _readiness.Tick(0f);
        }

        [Test]
        public void ASwordSheathed_DoesNotSwing_OnlyOnceDrawn()
        {
            Hold(Sword);

            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.NotDrawn));
            Draw();
            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.Attacking));
            Assert.That(_combat.TryAttack(CoreEquipSlots.LeftHand), Is.EqualTo(AttackOutcome.NoWeapon), "Nothing in the left hand.");
            _events.Flush();

            var started = _told.OfType<AttackStarted>().Single();
            Assert.That((started.Actor, started.Slot, started.Weapon, started.Move), Is.EqualTo((ActorId.Player, CoreEquipSlots.RightHand, Sword.Id, CombatMoveKind.Attack)));
        }

        [Test]
        public void ASwing_LandsOnlyInItsHitWindow_OncePerTarget_AndNeverOnItsSwinger()
        {
            Hold(Sword);
            Draw();
            _combat.TryAttack(CoreEquipSlots.RightHand);
            var swing = _combat.Swings.Single();

            Assert.That(_combat.Strike(swing, Dummy), Is.False, "Still winding up.");
            _combat.Tick(0.5f);
            Assert.That(swing.Phase, Is.EqualTo(SwingPhase.Striking));
            Assert.That(_combat.Strike(swing, ActorId.Player), Is.False, "Not itself.");
            Assert.That(_combat.Strike(swing, Dummy), Is.True);
            Assert.That(_combat.Strike(swing, Dummy), Is.False, "Once per swing.");
            Assert.That(_combat.Strike(swing, new ActorId("nobody")), Is.False, "Only who can be struck.");
            _events.Flush();

            Assert.That(_dummy.Current(CoreAttributes.Health), Is.EqualTo(18f));
            var hit = _told.OfType<HitLanded>().Single();
            Assert.That((hit.Attacker, hit.Target, hit.Weapon, hit.Damage), Is.EqualTo((ActorId.Player, Dummy, Sword.Id, 12f)));

            _combat.Tick(0.2f);
            Assert.That(swing.Phase, Is.EqualTo(SwingPhase.Recovery));
            Assert.That(_combat.Strike(swing, Dummy), Is.False, "Past its window.");
        }

        [Test]
        public void ASwing_PlaysOut_AndThatHandMaySwingAgain_ButNotBefore()
        {
            Hold(Sword);
            Draw();
            var ended = new List<Swing>();
            _combat.SwingEnded += ended.Add;
            _combat.TryAttack(CoreEquipSlots.RightHand);

            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.Busy));
            _combat.Tick(1f);

            Assert.That(_combat.Swings, Is.Empty);
            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.Attacking));
        }

        [Test]
        public void TwoSwords_SwingApart_ATwoHander_TakesBothHands()
        {
            Hold(Sword, CoreEquipSlots.RightHand);
            Hold(Sword, CoreEquipSlots.LeftHand);
            Draw();

            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.Attacking));
            Assert.That(_combat.TryAttack(CoreEquipSlots.LeftHand), Is.EqualTo(AttackOutcome.Attacking));
            Assert.That(_combat.Swings.Count, Is.EqualTo(2));

            Hold(Claymore);
            Assert.That(_combat.Swings, Is.Empty, "Taking the swords off stopped their swings.");
            Draw();
            Assert.That(_combat.TryAttack(CoreEquipSlots.LeftHand), Is.EqualTo(AttackOutcome.Attacking), "Either hand swings it.");
            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.Busy), "Both hands are in its swing.");
        }

        [Test]
        public void AMove_SpendsItsStamina_AndWithoutEnough_IsNotMade()
        {
            Hold(Claymore);
            Draw();

            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.Attacking));
            Assert.That(_attacker.Current(CoreAttributes.Stamina), Is.EqualTo(2f));
            _combat.Tick(1f);
            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.Tired));
        }

        [Test]
        public void ABlowThatEmptiesHealth_Kills_AndTheDeadAreStruckNoMore()
        {
            Hold(Sword);
            Draw();
            for (var i = 0; i < 3; i++)
            {
                _combat.TryAttack(CoreEquipSlots.RightHand);
                _combat.Tick(0.5f);
                _combat.Strike(_combat.Swings.Single(), Dummy);
                _combat.Tick(0.5f);
            }

            _events.Flush();

            Assert.That(_told.OfType<HitLanded>().Select(h => h.Damage), Is.EqualTo(new[] { 12f, 12f, 6f }), "The last takes what was left.");
            var killed = _told.OfType<ActorKilled>().Single();
            Assert.That((killed.Actor, killed.By), Is.EqualTo((Dummy, ActorId.Player)));
        }

        [Test]
        public void PuttingTheWeaponAway_StopsItsSwing()
        {
            Hold(Sword);
            Draw();
            _combat.TryAttack(CoreEquipSlots.RightHand);

            _readiness.Toggle();

            Assert.That(_combat.Swings, Is.Empty);
        }

        [Test]
        public void TheLook_TimesTheMove_ByItsAnimation()
        {
            Hold(Sword);
            Draw();
            _combat.Timings = new FixedTimings(new MoveTiming(2f, 0.1f, 0.2f), "SwordSlash");

            _combat.TryAttack(CoreEquipSlots.RightHand);

            Assert.That(_combat.Swings.Single().Timing.Seconds, Is.EqualTo(2f));
            Hold(Claymore);
            Draw();
            _combat.TryAttack(CoreEquipSlots.RightHand);
            Assert.That(_combat.Swings.Single().Timing.Seconds, Is.EqualTo(MoveTiming.Default.Seconds), "A move the look can't time.");
        }

        [Test]
        public void AnEffect_DamagesOrRestoresAResource_AndLeavesWhatItLacksAlone()
        {
            Assert.That(AttributeEffects.Apply(Effect.Damage(CoreAttributes.Health, 50f), _dummy), Is.EqualTo(30f));
            Assert.That(AttributeEffects.Apply(Effect.Restore(CoreAttributes.Health, 5f), _dummy), Is.EqualTo(5f));
            Assert.That(AttributeEffects.Apply(Effect.Damage(CoreAttributes.Magicka, 5f), _dummy), Is.Zero);
            Assert.That(_dummy.Current(CoreAttributes.Health), Is.EqualTo(5f));
        }

        [Test]
        public void APlacedObjectNamingAnActor_IsOneWhoCanBeStruck_ByItsPlacedId_AndAThingIsNot()
        {
            var dummy = ActorRecord.IdFor("forge.core", "training_dummy");
            var world = LoadOrderResolver.Resolve(new[]
            {
                Plugin("forge.core",
                    new AttributeRecord(CoreAttributes.Health, "Health", AttributeKind.Resource, 100f),
                    new ActorRecord(dummy, "Training Dummy"),
                    new BaseObjectRecord(new RecordId("forge.core", "training_dummy"), "Training Dummy", "props/training_dummy", actor: dummy),
                    new BaseObjectRecord(new RecordId("forge.core", "barrel"), "Barrel", "props/barrel"),
                    Placed("forge.core:ref/dummy", "forge.core:training_dummy"),
                    Placed("forge.core:ref/barrel", "forge.core:barrel", x: 2f)),
            }).Records;
            var combatants = new Combatants();
            var placed = new PlacedActors(combatants);

            Assert.That(placed.TryGet(world, RecordId.Parse("forge.core:ref/dummy"), out var actor), Is.True);
            Assert.That(actor, Is.EqualTo(PlacedActors.IdOf(RecordId.Parse("forge.core:ref/dummy"))));
            Assert.That(combatants.TryGet(actor, out var attributes) && attributes.Current(CoreAttributes.Health) == 100f, Is.True);
            Assert.That(combatants.NameOf(actor), Is.EqualTo("Training Dummy"));
            attributes.Drain(CoreAttributes.Health, 30f);
            placed.TryGet(world, RecordId.Parse("forge.core:ref/dummy"), out _);
            Assert.That(attributes.Current(CoreAttributes.Health), Is.EqualTo(70f), "Met again, it is the same, hurt as it was.");
            Assert.That(placed.TryGet(world, RecordId.Parse("forge.core:ref/barrel"), out _), Is.False, "A barrel is just a thing.");
        }

        sealed class FixedTimings : IMoveTimings
        {
            readonly MoveTiming _timing;
            readonly string _only;

            public FixedTimings(MoveTiming timing, string only = null)
            {
                _timing = timing;
                _only = only;
            }

            public bool TryGet(string animation, out MoveTiming timing)
            {
                timing = _timing;
                return _only == null || animation == _only;
            }
        }
    }
}
