using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Cameras;
using Forge.Game.Events;
using Forge.Game.Interaction;
using Forge.Game.Items;
using Forge.Game.World;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Game
{
    public class ReachRayTests
    {
        const float Reach = 2.5f;
        static readonly Vector3 Eyes = new Vector3(0f, 1.7f, 0f);

        static ViewPose Looking(Vector3 from, Vector3 at) => new ViewPose(from, LookRotation(Vector3.Normalize(at - from)));

        // The rotation turning +Z to a level-ish forward, as the cameras build theirs.
        static Quaternion LookRotation(Vector3 forward)
        {
            var yaw = MathF.Atan2(forward.X, forward.Z);
            var pitch = -MathF.Asin(forward.Y);
            return Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f);
        }

        [Test]
        public void FirstPerson_ReachesStraightAhead_FromTheEyes()
        {
            Assert.That(ReachRay.TryGet(Looking(Eyes, Eyes + Vector3.UnitZ), Eyes, Reach, out var from, out var direction, out var length), Is.True);

            Assert.That(Vector3.Distance(from, Eyes), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(direction, Vector3.UnitZ), Is.LessThan(1e-4f));
            Assert.That(length, Is.EqualTo(Reach).Within(1e-4f));
        }

        [Test]
        public void ThirdPerson_StartsLevelWithTheEyes_SoNothingBehindThePlayerIsInReach()
        {
            // 3.5 m behind and 0.35 m right of the eyes, looking the same way.
            var camera = Eyes + new Vector3(0.35f, 0f, -3.5f);

            Assert.That(ReachRay.TryGet(Looking(camera, camera + Vector3.UnitZ), Eyes, Reach, out var from, out _, out var length), Is.True);

            Assert.That(from.Z, Is.EqualTo(Eyes.Z).Within(1e-4f), "Level with the eyes.");
            Assert.That(length, Is.EqualTo(MathF.Sqrt(Reach * Reach - 0.35f * 0.35f)).Within(1e-3f), "To where the line leaves reach of the eyes.");
        }

        [Test]
        public void ALineThatNeverComesWithinReach_ReachesNothing()
        {
            var camera = Eyes + new Vector3(3f, 0f, -3.5f);

            Assert.That(ReachRay.TryGet(Looking(camera, camera + Vector3.UnitZ), Eyes, Reach, out _, out _, out _), Is.False);
            Assert.That(ReachRay.TryGet(Looking(Eyes, Eyes + Vector3.UnitZ), Eyes, 0f, out _, out _, out _), Is.False);
        }
    }

    public class InteractionFocusTests
    {
        static readonly RecordId Apple = RecordId.Parse("core:apple");
        static readonly RecordId Tree = RecordId.Parse("core:tree");
        static readonly RecordId AppleRef = RecordId.Parse("core:ref/apple");
        static readonly RecordId OtherAppleRef = RecordId.Parse("core:ref/apple2");
        static readonly RecordId TreeRef = RecordId.Parse("core:ref/tree");
        static readonly Vector3 Eyes = new Vector3(0f, 1.7f, 0f);
        static readonly ViewPose View = new ViewPose(Eyes, Quaternion.Identity);

        sealed class FakeProbe : IInteractionProbe
        {
            public readonly List<RecordId> InSight = new List<RecordId>();

            public void Find(Vector3 from, Vector3 direction, float length, IList<RecordId> found)
            {
                foreach (var id in InSight)
                    found.Add(id);
            }
        }

        // Counts the offers it is asked for, to show the focus doesn't ask again while nothing changed.
        sealed class CountingInteraction : IInteraction
        {
            public int Offers;

            public InteractionOffer Offer(InteractionTarget target, ActorId actor)
            {
                Offers++;
                return new InteractionOffer("Touch", target.Kind.DisplayName);
            }

            public void Perform(InteractionTarget target, ActorId actor)
            {
            }
        }

        static IRecordStore World() => LoadOrderResolver.Resolve(new[]
        {
            Plugin("core",
                new BaseObjectRecord(Apple, "Red Apple", "items/apple", item: new ItemPart(ItemKind.Food, 0.1f, 3)),
                new BaseObjectRecord(Tree, "Oak", "nature/oak"),
                Placed("core:ref/apple", "core:apple"),
                Placed("core:ref/apple2", "core:apple", x: 1f),
                Placed("core:ref/tree", "core:tree", x: 2f)),
        }).Records;

        sealed class Session
        {
            public readonly WorldChanges Changes = new WorldChanges();
            public readonly Inventory Pack = new Inventory();
            public readonly GameEvents Events = new GameEvents();
            public readonly FakeProbe Probe = new FakeProbe();
            public readonly InteractionFocus Focus;

            public Session(params IInteraction[] more)
            {
                var interactions = new Interactions().Add(new TakeInteraction(Changes, actor => actor == ActorId.Player ? Pack : null, Events));
                foreach (var interaction in more)
                    interactions.Add(interaction);
                Focus = new InteractionFocus(ActorId.Player, World(), interactions, Probe, 2.5f);
            }

            public void Look(params RecordId[] inSight)
            {
                Probe.InSight.Clear();
                Probe.InSight.AddRange(inSight);
                Focus.Update(View, Eyes);
            }
        }

        [Test]
        public void AnItemInSight_OffersToTakeIt_WithItsWeightAndValue()
        {
            var play = new Session();

            play.Look(AppleRef);

            Assert.That((play.Focus.Offer.Verb, play.Focus.Offer.Name, play.Focus.Offer.Detail), Is.EqualTo(("Take", "Red Apple", "Weight 0.1   Value 3")));
            Assert.That(play.Focus.Target.Placed.Id, Is.EqualTo(AppleRef));
        }

        [Test]
        public void Activating_TakesTheItem_OutOfTheWorld_IntoThePack_AndTellsTheStream()
        {
            var play = new Session();
            var taken = new List<ItemTaken>();
            play.Events.Subscribe<ItemTaken>(taken.Add);
            play.Look(AppleRef);

            Assert.That(play.Focus.Activate(), Is.True);
            play.Events.Flush();

            Assert.That(play.Changes.IsRemoved(AppleRef), Is.True);
            Assert.That(play.Pack.CountOf(Apple), Is.EqualTo(1));
            Assert.That(taken.Count, Is.EqualTo(1));
            Assert.That((taken[0].Actor, taken[0].Item, taken[0].From, taken[0].Count), Is.EqualTo((ActorId.Player, Apple, AppleRef, 1)));
            Assert.That(play.Focus.Offer, Is.Null, "What was used is let go.");
            Assert.That(play.Focus.Activate(), Is.False, "Nothing to use until the next look.");

            play.Look(AppleRef);
            Assert.That(play.Focus.Offer, Is.Null, "A taken item can't be taken again.");
        }

        [Test]
        public void TakingAnotherOfTheSameItem_Stacks()
        {
            var play = new Session();

            play.Look(AppleRef);
            play.Focus.Activate();
            play.Look(OtherAppleRef);
            play.Focus.Activate();

            Assert.That(play.Pack.Stacks.Count, Is.EqualTo(1));
            Assert.That(play.Pack.CountOf(Apple), Is.EqualTo(2));
        }

        [Test]
        public void WhatIsNoItem_OffersNothing_ButAnItemItGrazesStillCounts()
        {
            var play = new Session();

            play.Look(TreeRef);
            Assert.That(play.Focus.Offer, Is.Null);
            Assert.That(play.Focus.Activate(), Is.False);

            play.Look(TreeRef, AppleRef);
            Assert.That(play.Focus.Offer?.Name, Is.EqualTo("Red Apple"));
        }

        [Test]
        public void NothingInSight_OrUnknownIds_OfferNothing()
        {
            var play = new Session();
            play.Look(AppleRef);

            play.Look();
            Assert.That(play.Focus.Offer, Is.Null);

            play.Look(RecordId.Parse("core:ref/gone"), Apple);
            Assert.That(play.Focus.Offer, Is.Null, "Ids that are no placed object are passed over.");
        }

        [Test]
        public void TheFirstInteractionToOffer_IsTheOneTheUseKeyDoes()
        {
            var touch = new CountingInteraction();
            var play = new Session(touch);

            play.Look(AppleRef);
            Assert.That(play.Focus.Offer.Verb, Is.EqualTo("Take"), "Take comes first.");

            play.Look(TreeRef);
            Assert.That(play.Focus.Offer.Verb, Is.EqualTo("Touch"), "Take passes on what is no item; the next offers.");
        }

        [Test]
        public void SteadyLooking_DoesNotAskAgain_UntilTheTargetChangesOrARefresh()
        {
            var touch = new CountingInteraction();
            var play = new Session(touch);

            play.Look(TreeRef);
            var offer = play.Focus.Offer;
            play.Look(TreeRef);
            play.Look(TreeRef);

            Assert.That(touch.Offers, Is.EqualTo(1));
            Assert.That(play.Focus.Offer, Is.SameAs(offer));

            play.Focus.Refresh();
            play.Look(TreeRef);
            Assert.That(touch.Offers, Is.EqualTo(2));
        }

        [Test]
        public void AnActorThatCarriesNothing_IsOfferedNothingToTake()
        {
            var changes = new WorldChanges();
            var take = new TakeInteraction(changes, _ => null, new GameEvents());
            var records = World();
            InteractionTarget.TryResolve(records, AppleRef, out var apple);

            Assert.That(take.Offer(apple, new ActorId("npc")), Is.Null);
        }

        [Test]
        public void Focus_RequiresAnActor_AndAReach()
        {
            Assert.Throws<ArgumentException>(() => new InteractionFocus(default, World(), new Interactions(), new FakeProbe(), 2.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InteractionFocus(ActorId.Player, World(), new Interactions(), new FakeProbe(), 0f));
        }
    }

    public class WorldChangesTests
    {
        static readonly RecordId Ref = RecordId.Parse("core:ref/1");

        [Test]
        public void Remove_TakesAnObjectOut_Once_AndClearPutsTheWorldBack()
        {
            var changes = new WorldChanges();
            var removed = new List<RecordId>();
            var cleared = 0;
            changes.Removed += removed.Add;
            changes.Cleared += () => cleared++;

            Assert.That(changes.Remove(Ref), Is.True);
            Assert.That(changes.Remove(Ref), Is.False);
            Assert.That(changes.IsRemoved(Ref), Is.True);
            Assert.That(removed, Is.EqualTo(new[] { Ref }));

            changes.Clear();
            changes.Clear();
            Assert.That(changes.IsRemoved(Ref), Is.False);
            Assert.That(changes.RemovedObjects, Is.Empty);
            Assert.That(cleared, Is.EqualTo(1), "Clearing an unchanged world tells no one.");
            Assert.Throws<ArgumentException>(() => changes.Remove(default));
        }
    }

    public class InventoryTests
    {
        static readonly RecordId Apple = RecordId.Parse("core:apple");
        static readonly RecordId Gold = RecordId.Parse("core:gold");

        [Test]
        public void Add_Stacks_ByItem_InTheOrderFirstTaken()
        {
            var pack = new Inventory();

            pack.Add(Gold, 25);
            pack.Add(Apple);
            pack.Add(Gold, 5);

            Assert.That(pack.Stacks, Is.EqualTo(new[] { new ItemStack(Gold, 30), new ItemStack(Apple, 1) }));
            Assert.That(pack.CountOf(Gold), Is.EqualTo(30));
            Assert.That(pack.CountOf(RecordId.Parse("core:sword")), Is.Zero);
        }

        [Test]
        public void TryRemove_TakesSome_OrAll_ButNeverMoreThanCarried()
        {
            var pack = new Inventory();
            pack.Add(Gold, 3);

            Assert.That(pack.TryRemove(Gold, 2), Is.True);
            Assert.That(pack.CountOf(Gold), Is.EqualTo(1));
            Assert.That(pack.TryRemove(Gold, 2), Is.False);
            Assert.That(pack.CountOf(Gold), Is.EqualTo(1), "Nothing taken.");
            Assert.That(pack.TryRemove(Gold), Is.True);
            Assert.That(pack.Stacks, Is.Empty, "An empty stack is gone.");
            Assert.That(pack.TryRemove(Apple), Is.False);
        }

        [Test]
        public void Counts_ArePositive_AndItemsNamed()
        {
            var pack = new Inventory();

            Assert.Throws<ArgumentOutOfRangeException>(() => pack.Add(Apple, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => pack.TryRemove(Apple, -1));
            Assert.Throws<ArgumentException>(() => pack.Add(default));
        }
    }
}
