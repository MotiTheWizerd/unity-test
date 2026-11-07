using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Events;
using Forge.Game.Interaction;
using Forge.Game.Items;
using Forge.Game.Player;
using Forge.Game.World;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Game
{
    // A small world of items: an apple (0.5, worth 3), a sword (9, worth 75), a potion (0.5, worth 30) and a tree that is
    // no item, an apple and a tree placed.
    static class ItemWorld
    {
        public static readonly RecordId Apple = RecordId.Parse("core:apple");
        public static readonly RecordId Sword = RecordId.Parse("core:sword");
        public static readonly RecordId Potion = RecordId.Parse("core:potion");
        public static readonly RecordId Tree = RecordId.Parse("core:tree");
        public static readonly RecordId AppleRef = RecordId.Parse("core:ref/apple");
        public static readonly RecordId TreeRef = RecordId.Parse("core:ref/tree");

        public static IRecordStore Records() => LoadOrderResolver.Resolve(new[]
        {
            Plugin("core",
                new BaseObjectRecord(Apple, "Red Apple", "items/apple", item: new ItemPart(ItemKind.Food, 0.5f, 3)),
                new BaseObjectRecord(Sword, "Viking Sword", "items/sword", item: new ItemPart(ItemKind.Weapon, 9f, 75)),
                new BaseObjectRecord(Potion, "Healing Potion", "items/potion", item: new ItemPart(ItemKind.Potion, 0.5f, 30)),
                new BaseObjectRecord(Tree, "Oak", "nature/oak"),
                Placed("core:ref/apple", "core:apple"),
                Placed("core:ref/tree", "core:tree", x: 2f)),
        }).Records;

        public static PlacedObjectRecord Dropped(WorldChanges changes, RecordId item, float x = 0f) =>
            new PlacedObjectRecord(changes.NewId(), item, Space, new Vector3(x, 0f, 0f), Quaternion.Identity, 1f);
    }

    public class InventoryClearTests
    {
        [Test]
        public void Clear_TakesEverythingOut_AndTellsOnlyWhenSomethingWent()
        {
            var inventory = new Inventory();
            inventory.Add(ItemWorld.Sword);
            inventory.Add(ItemWorld.Apple, 3);
            var told = 0;
            inventory.Changed += () => told++;

            inventory.Clear();
            inventory.Clear();

            Assert.That(inventory.Stacks, Is.Empty);
            Assert.That(inventory.CountOf(ItemWorld.Sword), Is.Zero);
            Assert.That(told, Is.EqualTo(1), "An empty pack emptied again changes nothing.");
        }
    }

    public class WorldChangesAddedTests
    {
        [Test]
        public void Add_PutsAnObjectIn_AndRemovingItTakesItOutForGood()
        {
            var changes = new WorldChanges();
            var added = new List<PlacedObjectRecord>();
            var removed = new List<RecordId>();
            changes.Added += added.Add;
            changes.Removed += removed.Add;
            var apple = ItemWorld.Dropped(changes, ItemWorld.Apple);

            changes.Add(apple);
            Assert.That(added, Is.EqualTo(new[] { apple }));
            Assert.That(changes.TryGetAdded(apple.Id, out var found) && found == apple, Is.True);

            Assert.That(changes.Remove(apple.Id), Is.True);
            Assert.That(changes.Remove(apple.Id), Is.False, "Gone already.");
            Assert.That(removed, Is.EqualTo(new[] { apple.Id }));
            Assert.That(changes.AddedObjects, Is.Empty);
            Assert.That(changes.IsRemoved(apple.Id), Is.False, "What playing made is not remembered as taken: it simply is no more.");
        }

        [Test]
        public void NewIds_AreNeverGivenTwice_EvenAfterClear()
        {
            var changes = new WorldChanges();
            var first = changes.NewId();
            changes.Clear();

            Assert.That(changes.NewId(), Is.Not.EqualTo(first));
            Assert.That(first.Namespace, Is.EqualTo(WorldChanges.Namespace));
        }

        [Test]
        public void Add_TakesOnlyWhatPlayingMade_AndOnlyOnce()
        {
            var changes = new WorldChanges();
            var apple = ItemWorld.Dropped(changes, ItemWorld.Apple);
            changes.Add(apple);

            Assert.Throws<ArgumentException>(() => changes.Add(apple));
            Assert.Throws<ArgumentException>(() => changes.Add(Placed("core:ref/x", "core:apple")), "A plugin's object is never added.");
            Assert.Throws<ArgumentNullException>(() => changes.Add(null));
        }

        [Test]
        public void Clear_LetsWhatWasAddedGoToo()
        {
            var changes = new WorldChanges();
            var cleared = 0;
            changes.Cleared += () => cleared++;
            changes.Add(ItemWorld.Dropped(changes, ItemWorld.Apple));

            changes.Clear();

            Assert.That(changes.AddedObjects, Is.Empty);
            Assert.That(cleared, Is.EqualTo(1));
        }
    }

    public class PlayedWorldTests
    {
        static readonly ChunkCoord Origin = WorldGrid.ChunkOf(0f, 0f);

        [Test]
        public void WhatWasTaken_IsGone_AndWhatWasDropped_IsThere()
        {
            var built = ItemWorld.Records();
            var changes = new WorldChanges();
            var played = new PlayedWorld(built, changes);
            var dropped = ItemWorld.Dropped(changes, ItemWorld.Sword, x: 1f);

            changes.Remove(ItemWorld.AppleRef);
            changes.Add(dropped);

            Assert.That(played.Contains(ItemWorld.AppleRef), Is.False);
            Assert.That(played.TryGet(ItemWorld.AppleRef, out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() => played.GetSource(ItemWorld.AppleRef));
            Assert.That(played.Get<PlacedObjectRecord>(dropped.Id), Is.SameAs(dropped));
            Assert.That(played.GetSource(dropped.Id), Is.EqualTo(WorldChanges.Namespace));
            Assert.That(played.Count, Is.EqualTo(built.Count));
            Assert.That(played.OfType<PlacedObjectRecord>().Select(p => p.Id), Is.EquivalentTo(new[] { ItemWorld.TreeRef, dropped.Id }));
            Assert.That(played.All.Count(), Is.EqualTo(played.Count));
            Assert.That(played.TryGet(ItemWorld.Sword, out var sword) && sword is BaseObjectRecord, Is.True, "Everything else reads as built.");
        }

        [Test]
        public void ItsBlocks_HoldWhatWasDropped_SortedById_AndNotWhatWasTaken()
        {
            var built = ItemWorld.Records();
            var changes = new WorldChanges();
            var played = new PlayedWorld(built, changes);
            Assert.That(played.ObjectsIn(Space, Origin), Is.SameAs(built.ObjectsIn(Space, Origin)), "An unchanged block is handed on as built.");

            var dropped = ItemWorld.Dropped(changes, ItemWorld.Sword, x: 1f);
            changes.Add(dropped);
            changes.Remove(ItemWorld.AppleRef);

            Assert.That(played.ObjectsIn(Space, Origin).Select(p => p.Id), Is.EqualTo(new[] { ItemWorld.TreeRef, dropped.Id }.OrderBy(id => id)));
            Assert.That(played.ObjectChunks(Space), Does.Contain(Origin));
            Assert.That(played.ObjectsIn(OtherSpace, Origin), Is.Empty);
        }

        [Test]
        public void ADroppedObject_FarFromAnyBuiltOne_AddsItsChunk()
        {
            var changes = new WorldChanges();
            var played = new PlayedWorld(ItemWorld.Records(), changes);
            var far = new PlacedObjectRecord(changes.NewId(), ItemWorld.Apple, Space, new Vector3(5000f, 0f, 5000f), Quaternion.Identity, 1f);

            changes.Add(far);

            var chunk = WorldGrid.ChunkOf(5000f, 5000f);
            Assert.That(played.ObjectChunks(Space), Does.Contain(chunk));
            Assert.That(played.ObjectsIn(Space, chunk), Is.EqualTo(new[] { far }));
        }
    }

    public class InventoryWeightAndListTests
    {
        [Test]
        public void Weight_IsEveryStack_ByItsRecord_AndWhatIsNoItemWeighsNothing()
        {
            var pack = new Inventory();
            pack.Add(ItemWorld.Sword);
            pack.Add(ItemWorld.Apple, 3);
            pack.Add(ItemWorld.Tree);
            pack.Add(RecordId.Parse("gone:thing"));

            Assert.That(pack.Weight(ItemWorld.Records()), Is.EqualTo(10.5f).Within(1e-4f));
        }

        [Test]
        public void Changed_IsToldOnEveryAddAndRemove_ButNotARefusedOne()
        {
            var pack = new Inventory();
            var changes = 0;
            pack.Changed += () => changes++;

            pack.Add(ItemWorld.Apple, 2);
            pack.TryRemove(ItemWorld.Apple);
            pack.TryRemove(ItemWorld.Sword);

            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void TheList_IsByName_WithEachItemsRecord_AndATabListsOneKind()
        {
            var records = ItemWorld.Records();
            var pack = new Inventory();
            pack.Add(ItemWorld.Sword);
            pack.Add(ItemWorld.Apple, 3);
            pack.Add(ItemWorld.Potion, 2);
            pack.Add(RecordId.Parse("gone:thing"));

            var all = InventoryList.Of(pack, records);
            Assert.That(all.Select(e => e.Name), Is.EqualTo(new[] { "Healing Potion", "Red Apple", "Viking Sword" }), "By name; what the world has no record of is left out.");
            var apples = all[1];
            Assert.That((apples.Item, apples.Kind, apples.Count, apples.Weight, apples.Value), Is.EqualTo((ItemWorld.Apple, ItemKind.Food, 3, 0.5f, 3)));
            Assert.That(apples.TotalWeight, Is.EqualTo(1.5f).Within(1e-4f));

            Assert.That(InventoryList.Of(pack, records, ItemKind.Weapon).Select(e => e.Item), Is.EqualTo(new[] { ItemWorld.Sword }));
            Assert.That(InventoryList.Of(pack, records, ItemKind.Misc), Is.Empty);
            Assert.That(pack.CountOf(RecordId.Parse("gone:thing")), Is.EqualTo(1), "Still carried.");
        }
    }

    public class EncumbranceTests
    {
        const float Frame = 1f / 60f;

        static ActorAttributes Attributes(float carryWeight) => new ActorAttributes(
            AttributeSet.From(new[] { new AttributeRecord(CoreAttributes.CarryWeight, "Carry Weight", AttributeKind.Stat, carryWeight) }),
            ActorId.Player);

        sealed class FlatGround : IPlayerMotor
        {
            public Vector3 Position { get; private set; }

            public MotorContact Move(Vector3 displacement)
            {
                Position = new Vector3(Position.X + displacement.X, 0f, Position.Z + displacement.Z);
                return new MotorContact(true, false);
            }

            public void Teleport(Vector3 feet) => Position = feet;
        }

        [Test]
        public void CarryingMoreThanTheCarryWeight_Overburdens_AndTellsTheStream()
        {
            var pack = new Inventory();
            var events = new GameEvents();
            var told = new List<IGameEvent>();
            events.SubscribeAll(told.Add);
            using var burden = new Encumbrance(pack, ItemWorld.Records(), Attributes(10f), events);

            pack.Add(ItemWorld.Sword);
            Assert.That((burden.Carried, burden.Capacity, burden.Overburdened), Is.EqualTo((9f, 10f, false)));
            pack.Add(ItemWorld.Apple, 2);
            Assert.That(burden.Overburdened, Is.False, "Exactly the carry weight is still fine.");
            pack.Add(ItemWorld.Apple);
            Assert.That(burden.Overburdened, Is.True);
            pack.TryRemove(ItemWorld.Sword);
            Assert.That(burden.Overburdened, Is.False);
            events.Flush();

            Assert.That(told.Select(e => e.GetType()), Is.EqualTo(new[] { typeof(ActorOverburdened), typeof(ActorUnburdened) }));
            var over = (ActorOverburdened)told[0];
            Assert.That((over.Actor, over.Carried, over.Capacity), Is.EqualTo((ActorId.Player, 10.5f, 10f)));
        }

        [Test]
        public void AStrongerActor_CarriesMore_TheMomentItsCarryWeightRises()
        {
            var pack = new Inventory();
            var attributes = Attributes(5f);
            pack.Add(ItemWorld.Sword);
            using var burden = new Encumbrance(pack, ItemWorld.Records(), attributes);
            var changed = 0;
            burden.Changed += () => changed++;
            Assert.That(burden.Overburdened, Is.True, "Already over when it starts.");

            attributes.Add(new ModifierSource("potion"), new AttributeModifier(CoreAttributes.CarryWeight, ModifierOp.Add, 10f));

            Assert.That(burden.Overburdened, Is.False);
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void AnOverburdenedPlayer_Walks_RunOrSprintAsTold_ButStillJumps()
        {
            var pack = new Inventory();
            pack.Add(ItemWorld.Sword, 2);
            using var burden = new Encumbrance(pack, ItemWorld.Records(), Attributes(10f));
            var player = new PlayerCharacter(new FlatGround(), LocomotionSettings.Default, 1.65f);
            player.Limits.Add(burden);
            var sprint = new PlayerCommands(new Vector2(0f, 1f), Vector2.Zero, sprint: true, jump: false);

            for (var t = 0f; t < 2f; t += Frame)
                player.Tick(sprint, Frame);

            var velocity = player.Locomotion.Velocity;
            Assert.That(new Vector2(velocity.X, velocity.Z).Length(), Is.EqualTo(LocomotionSettings.Default.WalkSpeed).Within(1e-3f));
            Assert.That(burden.Allow(new PlayerCommands(Vector2.Zero, Vector2.Zero, false, jump: true)).Jump, Is.True);

            pack.TryRemove(ItemWorld.Sword);
            Assert.That(burden.Allow(sprint).Sprint, Is.True, "Unburdened, it runs as told again.");
        }

        [Test]
        public void Carrying_NeedsTheCarryWeightAttribute()
        {
            var noCarry = new ActorAttributes(
                AttributeSet.From(new[] { new AttributeRecord(CoreAttributes.JumpCost, "Jump Cost", AttributeKind.Stat, 1f) }), ActorId.Player);

            Assert.Throws<DataValidationException>(() => new Encumbrance(new Inventory(), ItemWorld.Records(), noCarry));
        }

        [Test]
        public void Disposing_StopsListening()
        {
            var pack = new Inventory();
            var burden = new Encumbrance(pack, ItemWorld.Records(), Attributes(1f));
            burden.Dispose();

            pack.Add(ItemWorld.Sword);

            Assert.That(burden.Overburdened, Is.False);
        }
    }

    public class ItemDropperTests
    {
        static readonly Vector3 Spot = new Vector3(1f, 0f, 1f);

        sealed class FakeProbe : IInteractionProbe
        {
            public RecordId InSight;

            public void Find(Vector3 from, Vector3 direction, float length, IList<RecordId> found) => found.Add(InSight);
        }

        [Test]
        public void Dropping_TakesOneOutOfThePack_IntoTheWorld_AndTellsTheStream()
        {
            var changes = new WorldChanges();
            var pack = new Inventory();
            var events = new GameEvents();
            var dropped = new List<ItemDropped>();
            events.Subscribe<ItemDropped>(dropped.Add);
            pack.Add(ItemWorld.Apple, 2);

            var placed = new ItemDropper(changes, _ => pack, events).Drop(ActorId.Player, ItemWorld.Apple, Space, Spot, Quaternion.Identity);
            events.Flush();

            Assert.That((placed.BaseId, placed.Space, placed.Position, placed.Scale), Is.EqualTo((ItemWorld.Apple, Space, Spot, 1f)));
            Assert.That(changes.AddedObjects, Is.EqualTo(new[] { placed }));
            Assert.That(pack.CountOf(ItemWorld.Apple), Is.EqualTo(1));
            Assert.That(dropped.Count, Is.EqualTo(1));
            Assert.That((dropped[0].Actor, dropped[0].Item, dropped[0].Placed, dropped[0].Count), Is.EqualTo((ActorId.Player, ItemWorld.Apple, placed.Id, 1)));
        }

        [Test]
        public void WhatIsNotCarried_IsNotDropped_AndABadPlaceLosesNothing()
        {
            var changes = new WorldChanges();
            var pack = new Inventory();
            var dropper = new ItemDropper(changes, _ => pack, new GameEvents());

            Assert.That(dropper.Drop(ActorId.Player, ItemWorld.Sword, Space, Spot, Quaternion.Identity), Is.Null);

            pack.Add(ItemWorld.Sword);
            Assert.Throws<ArgumentException>(() => dropper.Drop(ActorId.Player, ItemWorld.Sword, default, Spot, Quaternion.Identity));
            Assert.That(pack.CountOf(ItemWorld.Sword), Is.EqualTo(1));
            Assert.That(changes.AddedObjects, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => new ItemDropper(changes, _ => null, new GameEvents()).Drop(ActorId.Player, ItemWorld.Sword, Space, Spot, Quaternion.Identity));
        }

        [Test]
        public void ADroppedItem_CanBeTakenAgain_ThroughThePlayedWorld()
        {
            var changes = new WorldChanges();
            var pack = new Inventory();
            var events = new GameEvents();
            var probe = new FakeProbe();
            var interactions = new Interactions().Add(new TakeInteraction(changes, _ => pack, events));
            var focus = new InteractionFocus(ActorId.Player, new PlayedWorld(ItemWorld.Records(), changes), interactions, probe, 2.5f);
            var view = new Forge.Game.Cameras.ViewPose(new Vector3(0f, 1.7f, 0f), Quaternion.Identity);
            pack.Add(ItemWorld.Sword);

            var placed = new ItemDropper(changes, _ => pack, events).Drop(ActorId.Player, ItemWorld.Sword, Space, Spot, Quaternion.Identity);
            probe.InSight = placed.Id;
            focus.Update(view, view.Position);

            Assert.That(focus.Offer?.Name, Is.EqualTo("Viking Sword"));
            Assert.That(focus.Activate(), Is.True);
            Assert.That(pack.CountOf(ItemWorld.Sword), Is.EqualTo(1));
            Assert.That(changes.AddedObjects, Is.Empty);

            focus.Update(view, view.Position);
            Assert.That(focus.Offer, Is.Null, "Taken again, it is gone again.");
        }
    }
}
