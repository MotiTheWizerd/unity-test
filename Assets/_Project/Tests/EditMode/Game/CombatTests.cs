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

        static readonly RecordId Archery = CombatStyleRecord.IdFor("forge.core", "archery");

        static readonly BaseObjectRecord Sword = Weapon("sword", CoreEquipSlots.OneHanded(), OneHanded, 12f);
        static readonly BaseObjectRecord Claymore = Weapon("claymore", CoreEquipSlots.TwoHanded(), Greatsword, 20f);

        static readonly BaseObjectRecord Bow = new BaseObjectRecord(new RecordId("forge.core", "bow"), "bow", "items/bow",
            item: new ItemPart(ItemKind.Weapon, 3f, 50), equippable: new EquippablePart(CoreEquipSlots.Bow()),
            weapon: new WeaponPart(Archery, new[] { Effect.Damage(CoreAttributes.Health, 10f) }, CoreEquipSlots.Ammo));

        static readonly BaseObjectRecord Arrow = new BaseObjectRecord(new RecordId("forge.core", "arrow"), "arrow", "items/arrow",
            item: new ItemPart(ItemKind.Misc, 0.1f, 1), equippable: new EquippablePart(CoreEquipSlots.Ammunition()));

        // The draw: 1 s, the arrow on the string at 0.5, fully drawn at 1 (where the clip ends, as the real one does).
        static readonly MoveTiming DrawTiming = new MoveTiming(1f, 0.5f, 1f);

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
                    new EquipSlotRecord(CoreEquipSlots.Ammo, "Ammo", "Chest"),
                    new CombatStyleRecord(Archery, "Archery", new[] { new CombatMove(CombatMoveKind.Attack, "BowShot", held: true) }),
                    Bow,
                    Arrow,
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

        // A bow drawn, with `arrows` arrows equipped, the draw timed as DrawTiming; the loosed shots kept.
        List<Shot> ReadyBow(int arrows)
        {
            Hold(Bow);
            if (arrows > 0)
            {
                _inventory.Add(Arrow.Id, arrows);
                _equipment.TryEquip(Arrow);
            }

            Draw();
            _combat.Timings = new FixedTimings(DrawTiming);
            var shots = new List<Shot>();
            _combat.Loosed += shots.Add;
            return shots;
        }

        [Test]
        public void ABow_WithoutArrows_DoesNotDraw()
        {
            ReadyBow(0);

            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.NoAmmo));
        }

        [Test]
        public void ABowsDraw_IsHeldAtFullDraw_ForAsLongAsTheAttackIs()
        {
            ReadyBow(5);
            var held = new List<Swing>();
            _combat.SwingHeld += held.Add;

            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.Attacking));
            var draw = _combat.Swings.Single();
            _combat.Tick(0.6f);
            Assert.That(held, Is.Empty, "Still drawing.");
            _combat.Tick(0.6f);
            _combat.Tick(5f);

            Assert.That(held, Is.EqualTo(new[] { draw }), "Told once, as it came to full draw.");
            Assert.That((draw.IsHeldStill, draw.Pull, draw.IsOver), Is.EqualTo((true, 1f, false)));
            Assert.That(_combat.Strike(draw, Dummy), Is.False, "A bow strikes with what it shoots, not itself.");
        }

        [Test]
        public void LettingGo_AtFullDraw_LoosesOneArrow_AtFullPower_AndItStrikesOnce()
        {
            var shots = ReadyBow(5);
            _combat.TryAttack(CoreEquipSlots.RightHand);
            _combat.Tick(2f);

            Assert.That(_combat.Release(CoreEquipSlots.RightHand), Is.True);
            _combat.Tick(0.01f);

            var shot = shots.Single();
            Assert.That((shot.Power, shot.Ammo, shot.Weapon), Is.EqualTo((1f, Arrow, Bow)));
            Assert.That(_inventory.CountOf(Arrow.Id), Is.EqualTo(4), "One arrow used up.");
            Assert.That(_combat.Swings, Is.Empty, "The draw ends at full draw, so loosed, it is over.");
            Assert.That(_combat.Strike(shot, ActorId.Player), Is.False, "Never its shooter.");
            Assert.That(_combat.Strike(shot, Dummy), Is.True, "It lands after its draw is over.");
            Assert.That(_combat.Strike(shot, Dummy), Is.False, "Once.");
            _events.Flush();

            Assert.That(_dummy.Current(CoreAttributes.Health), Is.EqualTo(20f));
            var loosed = _told.OfType<ShotLoosed>().Single();
            Assert.That((loosed.Actor, loosed.Weapon, loosed.Ammo, loosed.Power), Is.EqualTo((ActorId.Player, Bow.Id, Arrow.Id, 1f)));
        }

        [Test]
        public void LettingGo_HalfDrawn_LoosesAWeakerShot_AndTooSoon_LoosesTheWeakest_OnceTheArrowIsOn()
        {
            var shots = ReadyBow(5);
            _combat.TryAttack(CoreEquipSlots.RightHand);
            _combat.Tick(0.75f);
            _combat.Release(CoreEquipSlots.RightHand);

            var half = ActorCombat.WeakestShot + (1f - ActorCombat.WeakestShot) * 0.5f;
            Assert.That(shots.Single().Power, Is.EqualTo(half).Within(1e-4f), "Halfway from the string to full draw.");
            _combat.Tick(0.01f);
            Assert.That(_combat.Swings, Is.Empty);

            _combat.TryAttack(CoreEquipSlots.RightHand);
            _combat.Tick(0.2f);
            _combat.Release(CoreEquipSlots.RightHand);
            Assert.That(shots.Count, Is.EqualTo(1), "The arrow isn't on the string yet.");
            _combat.Tick(0.4f);

            Assert.That(shots.Count, Is.EqualTo(2), "Loosed as soon as it was on.");
            Assert.That(shots[1].Power, Is.EqualTo(ActorCombat.WeakestShot).Within(1e-4f));
            _combat.Strike(shots[0], Dummy);
            Assert.That(_dummy.Current(CoreAttributes.Health), Is.EqualTo(30f - 10f * half).Within(1e-4f), "The blow is as strong as the shot.");
        }

        [Test]
        public void TheLastArrow_IsLoosed_ThenTheBowHasNone()
        {
            var shots = ReadyBow(1);
            _combat.TryAttack(CoreEquipSlots.RightHand);
            _combat.Tick(1f);
            _combat.Release(CoreEquipSlots.RightHand);
            _combat.Tick(0.01f);

            Assert.That(shots.Count, Is.EqualTo(1));
            Assert.That(_equipment.In(CoreEquipSlots.Ammo), Is.Null, "The last came off as it was used.");
            Assert.That(_combat.TryAttack(CoreEquipSlots.RightHand), Is.EqualTo(AttackOutcome.NoAmmo));
        }

        [Test]
        public void ReleasingTheOtherKey_DoesNotLooseTheDraw_AndPuttingTheBowAway_LoosesNothing()
        {
            var shots = ReadyBow(5);
            _combat.TryAttack(CoreEquipSlots.RightHand);
            _combat.Tick(2f);

            Assert.That(_combat.Release(CoreEquipSlots.LeftHand), Is.False, "Drawn by the other key.");
            _readiness.Toggle();

            Assert.That((shots.Count, _combat.Swings.Count, _inventory.CountOf(Arrow.Id)), Is.EqualTo((0, 0, 5)));
        }

        [Test]
        public void AFlight_FallsAsItGoes_AndPointsTheWayItMoves()
        {
            var flight = Flight.Toward(System.Numerics.Vector3.Zero, new System.Numerics.Vector3(0f, 0f, 10f), 50f);
            for (var i = 0; i < 100; i++)
                flight = flight.Step(0.01f);

            Assert.That(flight.Position.Z, Is.EqualTo(50f).Within(1e-3f), "One second at 50 m/s.");
            Assert.That(flight.Position.Y, Is.EqualTo(-Flight.Gravity / 2f).Within(0.06f), "Fallen half g t², to within a step's fall.");
            Assert.That(flight.Heading.Y, Is.LessThan(0f), "Pointing down now.");
            Assert.That(flight.Step(0f).Position, Is.EqualTo(flight.Position), "No time, no move.");
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

            public bool TryGet(string animation, bool shoots, out MoveTiming timing)
            {
                timing = _timing;
                return _only == null || animation == _only;
            }
        }
    }
}
