using System;
using System.Collections.Generic;
using Forge.Game.Actors;
using Forge.Game.Events;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class GameEventsTests
    {
        readonly struct Rang : IGameEvent
        {
            public Rang(int number) => Number = number;

            public int Number { get; }

            public override string ToString() => $"Rang({Number})";
        }

        readonly struct Knocked : IGameEvent
        {
            public Knocked(int number) => Number = number;

            public int Number { get; }

            public override string ToString() => $"Knocked({Number})";
        }

        [Test]
        public void Publishing_OnlyQueues_TheFlushTells()
        {
            var events = new GameEvents();
            var heard = new List<int>();
            events.Subscribe<Rang>(e => heard.Add(e.Number));

            events.Publish(new Rang(1));
            Assert.That(heard, Is.Empty);
            Assert.That(events.Pending, Is.EqualTo(1));

            events.Flush();
            Assert.That(heard, Is.EqualTo(new[] { 1 }));
            Assert.That(events.Pending, Is.Zero);

            events.Flush();
            Assert.That(heard, Is.EqualTo(new[] { 1 }), "Told once.");
        }

        [Test]
        public void EventsAreTold_InTheOrderPublished_WhateverTheirType()
        {
            var events = new GameEvents();
            var heard = new List<string>();
            events.Subscribe<Rang>(e => heard.Add(e.ToString()));
            events.Subscribe<Knocked>(e => heard.Add(e.ToString()));

            events.Publish(new Rang(1));
            events.Publish(new Knocked(2));
            events.Publish(new Rang(3));
            events.Flush();

            Assert.That(heard, Is.EqualTo(new[] { "Rang(1)", "Knocked(2)", "Rang(3)" }));
        }

        [Test]
        public void ListenersHear_InTheOrderTheySubscribed()
        {
            var events = new GameEvents();
            var heard = new List<string>();
            events.Subscribe<Rang>(_ => heard.Add("first"));
            events.Subscribe<Rang>(_ => heard.Add("second"));

            events.Publish(new Rang(1));
            events.Flush();

            Assert.That(heard, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void WhatListenersPublish_IsToldInTheSameFlush_AfterWhatCameBefore()
        {
            var events = new GameEvents();
            var heard = new List<string>();
            events.Subscribe<Rang>(e =>
            {
                heard.Add(e.ToString());
                if (e.Number == 1)
                    events.Publish(new Knocked(10));
            });
            events.Subscribe<Knocked>(e => heard.Add(e.ToString()));

            events.Publish(new Rang(1));
            events.Publish(new Rang(2));
            events.Flush();

            Assert.That(heard, Is.EqualTo(new[] { "Rang(1)", "Rang(2)", "Knocked(10)" }));
            Assert.That(events.Pending, Is.Zero);
        }

        [Test]
        public void AChainThatNeverEnds_IsCutOff_AndReported()
        {
            var errors = new List<Exception>();
            var events = new GameEvents(errors.Add);
            var told = 0;
            events.Subscribe<Rang>(e =>
            {
                told++;
                events.Publish(new Rang(e.Number + 1));
            });

            events.Publish(new Rang(0));
            events.Flush();

            Assert.That(told, Is.EqualTo(GameEvents.MaxPerFlush));
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(events.Pending, Is.Zero);

            // The stream still works afterwards, its queues in step.
            var heard = new List<int>();
            events.Subscribe<Knocked>(e => heard.Add(e.Number));
            events.Publish(new Knocked(7));
            events.Flush();
            Assert.That(heard, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void AListenerThatThrows_IsReported_AndTheOthersStillHear()
        {
            var errors = new List<Exception>();
            var events = new GameEvents(errors.Add);
            var heard = 0;
            events.Subscribe<Rang>(_ => throw new InvalidOperationException("broken listener"));
            events.Subscribe<Rang>(_ => heard++);

            events.Publish(new Rang(1));
            events.Flush();

            Assert.That(heard, Is.EqualTo(1));
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0].Message, Is.EqualTo("broken listener"));
        }

        [Test]
        public void WithNowhereToReport_ErrorsAreThrown_AfterEveryoneWasTold()
        {
            var events = new GameEvents();
            var heard = 0;
            events.Subscribe<Rang>(_ => throw new InvalidOperationException("one"));
            events.Subscribe<Rang>(_ => heard++);

            events.Publish(new Rang(1));
            events.Publish(new Rang(2));
            var thrown = Assert.Throws<AggregateException>(() => events.Flush());

            Assert.That(heard, Is.EqualTo(2));
            Assert.That(thrown.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(events.Pending, Is.Zero);
        }

        [Test]
        public void ADisposedSubscription_HearsNoMore_EvenMidFlush()
        {
            var events = new GameEvents();
            var heard = new List<string>();
            IDisposable second = null;
            events.Subscribe<Rang>(_ =>
            {
                heard.Add("first");
                second.Dispose();
            });
            second = events.Subscribe<Rang>(_ => heard.Add("second"));

            events.Publish(new Rang(1));
            events.Flush();

            Assert.That(heard, Is.EqualTo(new[] { "first" }));
            Assert.DoesNotThrow(() => second.Dispose(), "Disposing twice is harmless.");
        }

        [Test]
        public void ASubscriptionMadeMidFlush_HearsWhatComesAfter()
        {
            var events = new GameEvents();
            var late = new List<int>();
            events.Subscribe<Rang>(e =>
            {
                if (e.Number == 1)
                    events.Subscribe<Rang>(later => late.Add(later.Number));
            });

            events.Publish(new Rang(1));
            events.Publish(new Rang(2));
            events.Flush();

            Assert.That(late, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void SubscribingToAll_HearsEveryEvent_InOrder_AfterItsOwnListeners()
        {
            var events = new GameEvents();
            var log = new List<string>();
            events.Subscribe<Knocked>(_ => log.Add("own"));
            var all = events.SubscribeAll(e => log.Add(e.ToString()));

            events.Publish(new Rang(1));
            events.Publish(new Knocked(2));
            events.Flush();
            all.Dispose();
            events.Publish(new Rang(3));
            events.Flush();

            Assert.That(log, Is.EqualTo(new[] { "Rang(1)", "own", "Knocked(2)" }));
        }

        [Test]
        public void EachStream_KeepsItsOwnListeners()
        {
            var one = new GameEvents();
            var other = new GameEvents();
            var heard = 0;
            one.Subscribe<Rang>(_ => heard++);

            other.Publish(new Rang(1));
            other.Flush();

            Assert.That(heard, Is.Zero);
        }

        [Test]
        public void ActorIds_AreTheirKeys()
        {
            Assert.That(new ActorId("player"), Is.EqualTo(ActorId.Player));
            Assert.That(new ActorId("guard") == new ActorId("guard"), Is.True);
            Assert.That(default(ActorId).IsDefault, Is.True);
            Assert.Throws<ArgumentException>(() => new ActorId(" "));
        }
    }
}
