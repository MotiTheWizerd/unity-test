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
using Forge.Game.World;
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
            item: new ItemPart(ItemKind.Misc, 0.1f, 1), equippable: new EquippablePart(CoreEquipSlots.Ammunition()), ammo: new AmmoPart(0.2f));

        static readonly RecordId ShieldStyle = CombatStyleRecord.IdFor("forge.core", "shield");

        // Stops 60% of a blocked blow, half a stamina point for each point stopped.
        static readonly BaseObjectRecord Shield = new BaseObjectRecord(new RecordId("forge.core", "shield"), "shield", "items/shield",
            item: new ItemPart(ItemKind.Weapon, 12f, 30), equippable: new EquippablePart(CoreEquipSlots.OffHand()),
            weapon: new WeaponPart(ShieldStyle, new[] { Effect.Damage(CoreAttributes.Health, 3f) }), block: new BlockPart(0.6f, 0.5f));

        // A blow coming at the player from in front of it (the player faces +Z), and one from behind.
        static readonly System.Numerics.Vector3 FromTheFront = -System.Numerics.Vector3.UnitZ;
        static readonly System.Numerics.Vector3 FromBehind = System.Numerics.Vector3.UnitZ;

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
                    new CombatStyleRecord(Archery, "Archery", new[]
                    {
                        new CombatMove(CombatMoveKind.Attack, "BowShot", held: true, release: "BowRelease"),
                        new CombatMove(CombatMoveKind.Death, "BowDeath"),
                        new CombatMove(CombatMoveKind.Hit, "BowHit"),
                    }),
                    Bow,
                    Arrow,
                    new AttributeRecord(CoreAttributes.Health, "Health", AttributeKind.Resource, 30f),
                    new AttributeRecord(CoreAttributes.Stamina, "Stamina", AttributeKind.Resource, 10f),
                    new CombatStyleRecord(OneHanded, "One-Handed", new[] { new CombatMove(CombatMoveKind.Attack, "SwordSlash") }),
                    new CombatStyleRecord(Greatsword, "Two-Handed", new[] { new CombatMove(CombatMoveKind.Attack, "Cleave", staminaCost: 8f) }),
                    Sword,
                    Claymore,
                    new CombatStyleRecord(ShieldStyle, "Shield", new[]
                    {
                        new CombatMove(CombatMoveKind.Block, "ShieldBlock", held: true),
                        new CombatMove(CombatMoveKind.Blocked, "ShieldImpact"),
                    }),
                    Shield),
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
        public void ItsDeath_IsItsWeaponsStyles_WhenTheStyleHasOne()
        {
            Assert.That(_combat.DeathAnimation, Is.Null, "Nothing in hand: the look's own.");
            Hold(Sword);
            Assert.That(_combat.DeathAnimation, Is.Null, "The sword's style has no death here: the look's own.");
        }

        [Test]
        public void ItsDeath_IsTheBows_WithABowInHand()
        {
            Hold(Bow);

            Assert.That(_combat.DeathAnimation, Is.EqualTo("BowDeath"));
        }

        [Test]
        public void ItsHit_IsItsWeaponsStyles_ElseTheLooksOwn()
        {
            Assert.That(_combat.HitAnimation, Is.Null, "Nothing in hand: the look's own.");
            Hold(Sword);
            Assert.That(_combat.HitAnimation, Is.Null, "The sword's style has no hit here: the look's own.");
            Hold(Bow);
            Assert.That(_combat.HitAnimation, Is.EqualTo("BowHit"), "The bow's, with a bow in hand.");
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
        public void ABlow_IsToldAsItSwingsThroughItsCut_OnceASwing()
        {
            Hold(Sword);
            Draw();
            _combat.TryAttack(CoreEquipSlots.RightHand);

            _combat.Tick(0.1f);
            _events.Flush();
            Assert.That(_told.OfType<MoveWindowOpened>(), Is.Empty, "Still winding up.");

            _combat.Tick(0.4f);
            _combat.Tick(0.1f);
            _events.Flush();
            var swung = _told.OfType<MoveWindowOpened>().Single();
            Assert.That((swung.Actor, swung.Slot, swung.Weapon, swung.Measure), Is.EqualTo((ActorId.Player, CoreEquipSlots.RightHand, Sword.Id, MoveMeasure.Cut)));
            Assert.That(swung.Animation, Is.EqualTo(_combat.Swings.Single().Move.Animation));
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
                // The rest of the swing, and the moment the blow stopped it.
                _combat.Tick(0.5f + ActorCombat.ImpactSeconds);
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
        public void TheDrawPull_RisesWithTheString_AndDropsOnceLetGo()
        {
            ReadyBow(5);
            Assert.That(_combat.DrawPull, Is.Zero, "Nothing drawn.");
            _combat.TryAttack(CoreEquipSlots.RightHand);

            _combat.Tick(0.4f);
            Assert.That(_combat.DrawPull, Is.Zero, "The arrow not yet on the string.");
            _combat.Tick(0.35f);
            Assert.That(_combat.DrawPull, Is.EqualTo(0.5f).Within(1e-4f));
            _combat.Tick(2f);
            Assert.That(_combat.DrawPull, Is.EqualTo(1f), "Held at full draw.");

            _combat.Release(CoreEquipSlots.RightHand);
            Assert.That(_combat.DrawPull, Is.Zero, "Loosed.");
        }

        [Test]
        public void ASwordsSwing_IsNoDraw()
        {
            Hold(Sword);
            Draw();
            _combat.TryAttack(CoreEquipSlots.RightHand);
            _combat.Tick(0.5f);

            Assert.That(_combat.DrawPull, Is.Zero);
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
            Assert.That(loosed.Release, Is.EqualTo("BowRelease"), "It let go by its move's release.");
            Assert.That(_told.OfType<MoveWindowOpened>().Single().Measure, Is.EqualTo(MoveMeasure.Draw), "The arrow came on the string once.");
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
        public void ADrawLetGo_LastsAsLongAsItsReleasePlays_StillAiming_ThenIsOver()
        {
            var shots = ReadyBow(5);
            _combat.Timings = new FixedTimings(DrawTiming) { Release = 0.5f };
            var ended = new List<Swing>();
            _combat.SwingEnded += ended.Add;
            _combat.TryAttack(CoreEquipSlots.RightHand);
            _combat.Tick(2f);

            _combat.Release(CoreEquipSlots.RightHand);
            Assert.That(shots.Count, Is.EqualTo(1), "Loosed at once, as the release starts.");
            _combat.Tick(0.3f);
            Assert.That((_combat.Aiming, ended.Count), Is.EqualTo((true, 0)), "Still in its release.");
            Assert.That(_combat.Swings.Single().ReleaseSeconds, Is.EqualTo(0.5f));

            _combat.Tick(0.25f);
            Assert.That((_combat.Aiming, ended.Count), Is.EqualTo((false, 1)), "Played out.");
        }

        [Test]
        public void AReleaseNamesAnAnimation_AndOnlyAHeldMoveHasOne()
        {
            Assert.That(new CombatMove(CombatMoveKind.Attack, "BowShot", held: true, release: "BowRelease").Release, Is.EqualTo("BowRelease"));
            Assert.That(new CombatMove(CombatMoveKind.Attack, "BowShot", held: true).Release, Is.Null);
            Assert.Throws<System.ArgumentException>(() => new CombatMove(CombatMoveKind.Attack, "SwordSlash", release: "BowRelease"));
            Assert.Throws<System.ArgumentException>(() => new CombatMove(CombatMoveKind.Attack, "BowShot", held: true, release: " "));
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
        public void ALandedArrow_StaysInTheWorldAsAnArrow_ToBeTakenBack_UnlessItBreaks()
        {
            var shots = ReadyBow(5);
            for (var i = 0; i < 2; i++)
            {
                _combat.TryAttack(CoreEquipSlots.RightHand);
                _combat.Tick(1f);
                _combat.Release(CoreEquipSlots.RightHand);
                _combat.Tick(0.01f);
            }

            var world = new WorldChanges();
            var rolls = new Queue<float>(new[] { 0.5f, 0.1f });
            var landings = new ShotLandings(world, _events, () => rolls.Dequeue());
            var space = new RecordId("forge.core", "space/main");
            var at = new System.Numerics.Vector3(1f, 2f, 3f);
            var turned = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX, 0.5f);

            var kept = landings.Land(shots[0], space, at, turned);
            var broken = landings.Land(shots[1], space, at, turned);
            _events.Flush();

            Assert.That((kept.BaseId, kept.Space, kept.Position, kept.Rotation), Is.EqualTo((Arrow.Id, space, at, turned)), "Rolled 0.5 against a fifth: it stays, as it landed.");
            Assert.That(world.AddedObjects, Is.EqualTo(new[] { kept }), "In the world, takeable as any item is.");
            Assert.That(broken, Is.Null, "Rolled 0.1: it broke.");
            var landed = _told.OfType<ShotLanded>().ToArray();
            Assert.That(landed.Select(l => (l.Actor, l.Ammo, l.Placed)),
                Is.EqualTo(new[] { (ActorId.Player, Arrow.Id, (RecordId?)kept.Id), (ActorId.Player, Arrow.Id, (RecordId?)null) }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AmmoPart(1.5f));
        }

        [Test]
        public void AnArrowStuckInSomeone_IsTheirsToCarry_UnlessItBreaks()
        {
            var shots = ReadyBow(5);
            for (var i = 0; i < 2; i++)
            {
                _combat.TryAttack(CoreEquipSlots.RightHand);
                _combat.Tick(1f);
                _combat.Release(CoreEquipSlots.RightHand);
                _combat.Tick(0.01f);
            }

            var rolls = new Queue<float>(new[] { 0.5f, 0.1f });
            var landings = new ShotLandings(new WorldChanges(), _events, () => rolls.Dequeue());
            var carried = new Inventory();

            Assert.That(landings.Lodge(shots[0], Dummy, carried), Is.True, "Rolled 0.5 against a fifth: it stays in them.");
            Assert.That(landings.Lodge(shots[1], Dummy, carried), Is.False, "Rolled 0.1: it broke.");
            _events.Flush();

            Assert.That(carried.CountOf(Arrow.Id), Is.EqualTo(1), "The one that stayed is theirs.");
            var landed = _told.OfType<ShotLanded>().ToArray();
            Assert.That(landed.Select(l => (l.Actor, l.Placed, l.LodgedIn, l.Broke)),
                Is.EqualTo(new[] { (ActorId.Player, (RecordId?)null, (ActorId?)Dummy, false), (ActorId.Player, (RecordId?)null, (ActorId?)null, true) }));
        }

        [Test]
        public void AHit_TellsWhereOnTheBodyItMet()
        {
            var shots = ReadyBow(5);
            _combat.TryAttack(CoreEquipSlots.RightHand);
            _combat.Tick(2f);
            _combat.Release(CoreEquipSlots.RightHand);
            _combat.Tick(0.01f);

            Assert.That(_combat.Strike(shots.Single(), Dummy, null, BlowMet.Body, BodyPart.Head), Is.True);
            _events.Flush();

            Assert.That(_told.OfType<HitLanded>().Single().Part, Is.EqualTo(BodyPart.Head));
        }

        // The player with a sword and a shield drawn, its shield raised (the left hand used, held into its window), a
        // guard facing +Z; and the dummy, fighting back with a sword.
        ActorCombat RaiseShieldAgainstAFoe()
        {
            Hold(Sword, CoreEquipSlots.RightHand);
            Hold(Shield);
            Draw();
            _combatants.Add(ActorId.Player, _attacker, "You", _combat);
            _combat.Facing = () => System.Numerics.Vector3.UnitZ;
            Assert.That(_combat.TryUse(CoreEquipSlots.LeftHand), Is.EqualTo(AttackOutcome.Attacking), "A shield's use is its block.");
            _combat.Tick(1f);

            var inventory = new Inventory();
            var equipment = new ActorEquipment(Dummy, inventory, _events);
            inventory.Add(Sword.Id);
            equipment.TryEquip(Sword);
            var readiness = new WeaponReadiness(equipment, _world, _events);
            readiness.SetTimes(0f, 0f);
            readiness.Toggle();
            readiness.Tick(0f);
            var foe = new ActorCombat(equipment, readiness, _world, null, _combatants, _events)
            {
                Timings = new FixedTimings(new MoveTiming(1f, 0.4f, 0.6f)),
            };
            foe.TryAttack(CoreEquipSlots.RightHand);
            foe.Tick(0.5f);
            return foe;
        }

        [Test]
        public void AShield_IsRaisedByItsHandsUse_HeldWhileTheKeyIs_AndStrikesNoOne()
        {
            Hold(Shield);
            Draw();

            Assert.That(_combat.TryAttack(CoreEquipSlots.LeftHand), Is.EqualTo(AttackOutcome.NoMove), "A shield doesn't attack...");
            Assert.That(_combat.TryUse(CoreEquipSlots.LeftHand), Is.EqualTo(AttackOutcome.Attacking), "...it blocks.");
            var block = _combat.Swings.Single();
            Assert.That((block.Guards, block.Move.Held, _combat.Guard), Is.EqualTo((true, true, (Swing)null)), "Not up yet.");
            _combat.Tick(3f);

            Assert.That(_combat.Guard, Is.SameAs(block), "Up, and held.");
            Assert.That(_combat.Strike(block, Dummy), Is.False, "A raised shield strikes no one.");
            _combat.Release(CoreEquipSlots.LeftHand);
            _combat.Tick(0.01f);
            Assert.That(_combat.Guard, Is.Null, "Lowered once let go...");
            _combat.Tick(0.5f);
            Assert.That(_combat.Swings, Is.Empty, "...and over once the rest of its clip has played.");
        }

        [Test]
        public void ARaisedShield_StopsItsShareOfABlowFromTheFront_ForStamina_AndIsJolted()
        {
            var foe = RaiseShieldAgainstAFoe();
            var jolts = new List<CombatMove>();
            _combat.Blocked += (_, reaction) => jolts.Add(reaction);

            Assert.That(foe.Strike(foe.Swings.Single(), ActorId.Player, FromTheFront), Is.True);
            _events.Flush();

            Assert.That(_attacker.Current(CoreAttributes.Health), Is.EqualTo(30f - 12f * 0.4f).Within(1e-4f), "60% of 12 stopped.");
            Assert.That(_attacker.Current(CoreAttributes.Stamina), Is.EqualTo(10f - 12f * 0.6f * 0.5f).Within(1e-4f), "Half a point per point stopped.");
            var blocked = _told.OfType<HitBlocked>().Single();
            Assert.That((blocked.Attacker, blocked.Target, blocked.Weapon, blocked.Guard), Is.EqualTo((Dummy, ActorId.Player, Sword.Id, Shield.Id)),
                "Told with what blocked it, for how it sounds.");
            Assert.That(blocked.Stopped, Is.EqualTo(7.2f).Within(1e-4f));
            var landed = _told.OfType<HitLanded>().Single();
            Assert.That(landed.Damage, Is.EqualTo(4.8f).Within(1e-4f), "What got through.");
            Assert.That(landed.Blocked, Is.True, "Past a guard: heard on the shield, not the body.");
            Assert.That(jolts.Single().Animation, Is.EqualTo("ShieldImpact"), "The style's reaction.");
            Assert.That(_combat.Guard, Is.Not.Null, "Still up.");
        }

        [Test]
        public void ABlowFromBehind_GetsPastTheShield()
        {
            var foe = RaiseShieldAgainstAFoe();

            foe.Strike(foe.Swings.Single(), ActorId.Player, FromBehind);
            _events.Flush();

            Assert.That(_attacker.Current(CoreAttributes.Health), Is.EqualTo(18f).Within(1e-4f), "All 12.");
            Assert.That(_attacker.Current(CoreAttributes.Stamina), Is.EqualTo(10f), "Nothing spent.");
            Assert.That(_told.OfType<HitBlocked>(), Is.Empty);
            Assert.That(_told.OfType<HitLanded>().Single().Blocked, Is.False);
        }

        [Test]
        public void TooTiredToHoldIt_TheGuardBreaks_AndTheBlowLandsWhole()
        {
            var foe = RaiseShieldAgainstAFoe();
            _attacker.Drain(CoreAttributes.Stamina, 8f);

            foe.Strike(foe.Swings.Single(), ActorId.Player, FromTheFront);
            _events.Flush();

            Assert.That(_attacker.Current(CoreAttributes.Health), Is.EqualTo(18f).Within(1e-4f));
            var broken = _told.OfType<GuardBroken>().Single();
            Assert.That((broken.Actor, broken.By), Is.EqualTo((ActorId.Player, Dummy)));
            Assert.That((_combat.Guard, _combat.Swings.Count), Is.EqualTo(((Swing)null, 0)), "The shield is down.");
        }

        [Test]
        public void AShieldNotYetUp_StopsNothing()
        {
            Hold(Shield);
            Draw();
            _combatants.Add(ActorId.Player, _attacker, "You", _combat);
            _combat.TryUse(CoreEquipSlots.LeftHand);

            Assert.That(_combat.Block(Dummy, Sword.Id, 12f, FromTheFront), Is.Zero, "Still coming up.");
            _combat.Tick(1f);
            Assert.That(_combat.Block(Dummy, Sword.Id, 12f, null), Is.EqualTo(0.6f), "Up: from wherever, without a facing.");
        }

        [Test]
        public void WhereTheBlowMet_Decides_TheShieldItselfStopsIt_TheBodyPastItDoesNot()
        {
            Hold(Shield);
            Draw();
            _combat.Facing = () => System.Numerics.Vector3.UnitZ;
            _combat.TryUse(CoreEquipSlots.LeftHand);
            _combat.Tick(1f);

            Assert.That(_combat.Block(Dummy, Sword.Id, 12f, FromBehind, BlowMet.Guard), Is.EqualTo(0.6f), "On the shield itself, whichever way it came.");
            Assert.That(_combat.Block(Dummy, Sword.Id, 12f, FromTheFront, BlowMet.Body), Is.Zero, "On the body past it, even from the front.");
            Assert.That(_combat.Block(Dummy, Sword.Id, 12f, FromBehind), Is.Zero, "Unseen: the arc decides.");
        }

        [Test]
        public void ABlowThatLands_StopsTheSwingAMoment_ThenItCutsOn()
        {
            Hold(Sword);
            Draw();
            var stalls = new List<float>();
            _combat.Stalled += stalls.Add;
            _combat.TryAttack(CoreEquipSlots.RightHand);
            var swing = _combat.Swings.Single();
            _combat.Tick(0.5f);

            Assert.That(_combat.Strike(swing, Dummy), Is.True);
            Assert.That((swing.Stalled, swing.Recoiling), Is.EqualTo((true, false)), "Through the one it strikes it stops a moment, and cuts on.");
            Assert.That(stalls.Single(), Is.EqualTo(ActorCombat.ImpactSeconds));
            _combat.Tick(ActorCombat.ImpactSeconds * 0.5f);
            Assert.That(swing.Elapsed, Is.EqualTo(0.5f).Within(1e-5f), "Time passes; it doesn't come on.");
            _combat.Tick(ActorCombat.ImpactSeconds);
            Assert.That(swing.Stalled, Is.False);
            Assert.That(swing.Elapsed, Is.EqualTo(0.5f + ActorCombat.ImpactSeconds * 0.5f).Within(1e-5f), "Then on again.");
        }

        [Test]
        public void ABladeTheShieldStops_JoltsTheOneBehindIt_AndComesBack_ItsBlowSpent()
        {
            var foe = RaiseShieldAgainstAFoe();
            var swing = foe.Swings.Single();
            var recoiled = new List<Swing>();
            foe.Recoiled += recoiled.Add;
            var jolted = new List<float>();
            _combat.Stalled += jolted.Add;

            Assert.That(foe.Strike(swing, ActorId.Player, FromTheFront), Is.True);

            Assert.That(recoiled.Single(), Is.SameAs(swing));
            Assert.That((swing.Recoiling, swing.IsStriking), Is.EqualTo((true, false)), "Its window closed: it strikes no one else.");
            Assert.That(jolted.Single(), Is.EqualTo(ActorCombat.ImpactSeconds), "The one behind the shield feels it too.");
            foe.Tick(ActorCombat.ImpactSeconds + ActorCombat.RecoilSeconds - 0.01f);
            Assert.That(foe.Swings, Has.Count.EqualTo(1), "Still coming back...");
            foe.Tick(0.02f);
            Assert.That(foe.Swings, Is.Empty, "...then over, sooner than the swing would have played out.");
        }

        [Test]
        public void WhatOfABlowGetsPastTheGuard_TheStruckOneGivesWith_WhichWayItCame_TheStrikerNot()
        {
            var foe = RaiseShieldAgainstAFoe();
            var struck = new List<Blow>();
            _combat.Struck += struck.Add;
            var foeStruck = new List<Blow>();
            foe.Struck += foeStruck.Add;

            foe.Strike(foe.Swings.Single(), ActorId.Player, FromTheFront);

            Assert.That(struck.Single().Reached, Is.EqualTo(0.4f).Within(1e-5f), "The shield stopped 60%.");
            Assert.That(struck.Single().Direction, Is.EqualTo(FromTheFront));
            Assert.That(struck.Single().Seconds, Is.EqualTo(ActorCombat.ImpactSeconds));
            Assert.That(foeStruck, Is.Empty, "The one who struck stops a moment, but isn't struck.");
        }

        [Test]
        public void ABlowFromBehind_ReachesTheBodyWhole()
        {
            var foe = RaiseShieldAgainstAFoe();
            var struck = new List<Blow>();
            _combat.Struck += struck.Add;

            foe.Strike(foe.Swings.Single(), ActorId.Player, FromBehind);

            Assert.That(struck.Single().Reached, Is.EqualTo(1f));
        }

        [Test]
        public void ABlowTheGuardStopsWhole_JoltsButIsNotFelt_OnTheBody()
        {
            var stalls = new List<float>();
            _combat.Stalled += stalls.Add;
            var struck = new List<Blow>();
            _combat.Struck += struck.Add;

            _combat.Jolt(new Blow(ActorCombat.ImpactSeconds, FromTheFront, 0f));

            Assert.That(stalls.Single(), Is.EqualTo(ActorCombat.ImpactSeconds), "The impact stops it a moment...");
            Assert.That(struck, Is.Empty, "...but nothing reached the body.");
        }

        [Test]
        public void ABlow_IsFeltForNoLessThanNothing_AndReachesAShareOfTheBody()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Blow(-1f, null, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Blow(0.1f, null, 1.5f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Blow(0.1f, null, float.NaN));
            Assert.That((new Blow(0f, null, 0f).ReachedBody, new Blow(0f, null, 0.2f).ReachedBody), Is.EqualTo((false, true)));
        }

        [Test]
        public void ABladeMeetingAWall_StopsThere_AndComesBack_OnlyInItsWindow_Once()
        {
            Hold(Sword);
            Draw();
            var recoiled = new List<Swing>();
            _combat.Recoiled += recoiled.Add;
            _combat.TryAttack(CoreEquipSlots.RightHand);
            var swing = _combat.Swings.Single();

            Assert.That(_combat.Bounce(swing), Is.False, "Still winding up: it meets nothing yet.");
            _combat.Tick(0.5f);
            Assert.That(_combat.Bounce(swing), Is.True);
            Assert.That((swing.Stalled, swing.Recoiling, recoiled.Single()), Is.EqualTo((true, true, swing)));
            Assert.That(_combat.Strike(swing, Dummy), Is.False, "Its blow is spent on the wall.");
            Assert.That(_combat.Bounce(swing), Is.False, "Once.");
        }

        [Test]
        public void ABlockPart_IsAFraction_ForSomeEffort_AndOnlyAWeaponHasOne()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BlockPart(1.5f, 0.5f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BlockPart(0.5f, -1f));
            Assert.Throws<System.ArgumentException>(() => new BaseObjectRecord(new RecordId("forge.core", "plank"), "plank", "items/plank",
                item: new ItemPart(ItemKind.Misc, 1f, 1), equippable: new EquippablePart(CoreEquipSlots.OffHand()), block: new BlockPart(0.5f, 0.5f)));
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

        [Test]
        public void ALead_AllowsForTheDrop_SoTheShotFallsOntoItsMark()
        {
            var from = new System.Numerics.Vector3(2f, 1.6f, -3f);
            foreach (var mark in new[]
                     {
                         from + new System.Numerics.Vector3(0f, -0.3f, 20f), // a chest, level ground
                         from + new System.Numerics.Vector3(-25f, 6f, 10f), // up a slope
                         from + new System.Numerics.Vector3(4f, -2f, -3f), // close, below
                     })
            {
                var way = Flight.Lead(from, mark, Shot.FullDrawSpeed);
                Assert.That(way.Length(), Is.EqualTo(1f).Within(1e-4f));
                var flight = new Flight(from, way * Shot.FullDrawSpeed);
                var across = new System.Numerics.Vector2(mark.X - from.X, mark.Z - from.Z).Length();
                while (new System.Numerics.Vector2(flight.Position.X - from.X, flight.Position.Z - from.Z).Length() < across)
                    flight = flight.Step(0.0005f);
                Assert.That(System.Numerics.Vector3.Distance(flight.Position, mark), Is.LessThan(0.08f), $"Onto {mark}.");
                Assert.That(way.Y, Is.GreaterThan(System.Numerics.Vector3.Normalize(mark - from).Y), "Aimed above the mark.");
            }
        }

        [Test]
        public void ALead_BeyondReach_LoosesAt45Degrees_AndStraightUpOrDown_StraightAtIt()
        {
            var far = Flight.Lead(System.Numerics.Vector3.Zero, new System.Numerics.Vector3(1000f, 0f, 0f), Shot.FullDrawSpeed);
            Assert.That(far.X, Is.EqualTo(far.Y).Within(1e-4f));
            Assert.That(far.X, Is.GreaterThan(0f));

            Assert.That(Flight.Lead(System.Numerics.Vector3.Zero, new System.Numerics.Vector3(0f, 5f, 0f), 10f), Is.EqualTo(System.Numerics.Vector3.UnitY));
            Assert.That(Flight.Lead(System.Numerics.Vector3.Zero, new System.Numerics.Vector3(0f, -5f, 0f), 10f), Is.EqualTo(-System.Numerics.Vector3.UnitY));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Flight.Lead(System.Numerics.Vector3.Zero, System.Numerics.Vector3.UnitX, 0f));
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

            /// <summary>How long a release plays; null: the look can't time one.</summary>
            public float? Release { get; set; }

            public bool TryGet(string animation, MoveMeasure measure, out MoveTiming timing)
            {
                timing = _timing;
                return _only == null || animation == _only;
            }

            public bool TryGetRelease(string animation, out float seconds)
            {
                seconds = Release ?? 0f;
                return Release.HasValue;
            }
        }
    }
}
