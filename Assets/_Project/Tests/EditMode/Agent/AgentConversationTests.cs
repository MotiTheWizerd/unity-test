using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Forge.Agent;
using Forge.Agent.Tools;
using Forge.Data.Records;
using Forge.Tests.Editing;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Forge.Tests.Agent
{
    public class AgentConversationTests
    {
        delegate Task Behaviour(AgentTurn turn, Action<AgentEvent> report, CancellationToken cancellation, AgentToolHost tools);

        sealed class FakeBackend : IAgentBackend
        {
            readonly Func<AgentTurn, Action<AgentEvent>, CancellationToken, Task> _run;
            readonly List<AgentTurn> _turns = new List<AgentTurn>();

            public FakeBackend(Func<AgentTurn, Action<AgentEvent>, CancellationToken, Task> run) => _run = run;

            public IReadOnlyList<AgentTurn> Turns
            {
                get
                {
                    lock (_turns)
                        return _turns.ToList();
                }
            }

            public bool Disposed { get; private set; }

            public Task RunAsync(AgentTurn turn, Action<AgentEvent> report, CancellationToken cancellation)
            {
                lock (_turns)
                    _turns.Add(turn);
                return _run(turn, report, cancellation);
            }

            public void Dispose() => Disposed = true;
        }

        /// <summary>A conversation over the building fixture's world, with a scripted backend.</summary>
        sealed class Rig : IDisposable
        {
            public Rig(Behaviour behaviour)
            {
                World = BuildingFixture.Create();
                Queue = new MainThreadQueue();
                Tools = new AgentToolHost(new ToolInvoker(World.Registry), Queue);
                Backend = new FakeBackend((turn, report, cancellation) => behaviour(turn, report, cancellation, Tools));
                Conversation = new AgentConversation(Backend, Tools, World.Session, Queue, () => "Camera looks at (20, 20).", "Build well.");
            }

            public BuildingFixture World { get; }

            public MainThreadQueue Queue { get; }

            public AgentToolHost Tools { get; }

            public FakeBackend Backend { get; }

            public AgentConversation Conversation { get; }

            public int Placed => World.Session.World.Records.OfType<PlacedObjectRecord>().Count();

            /// <summary>Pumps the main thread, as the editor does every frame, until the request is over.</summary>
            public void Settle() => PumpUntil(() => Conversation.State != AgentState.Working);

            public void PumpUntil(Func<bool> condition)
            {
                var clock = Stopwatch.StartNew();
                while (!condition())
                {
                    Assert.That(clock.ElapsedMilliseconds, Is.LessThan(5000), "Timed out waiting for the agent.");
                    Queue.Pump();
                    Thread.Sleep(1);
                }

                Queue.Pump();
            }

            public void Dispose() => Conversation.Dispose();
        }

        static JObject House(float x) =>
            new JObject { ["objects"] = new JArray(new JObject { ["object"] = "core:house", ["x"] = x, ["z"] = 20f }) };

        static Behaviour BuildsAHouse(float x = 20f) => async (turn, report, cancellation, tools) =>
        {
            report(AgentEvent.ConversationStarted("c1"));
            await tools.CallAsync("place_objects", House(x));
            report(AgentEvent.Said("Built a house."));
            report(AgentEvent.Done());
        };

        [Test]
        public void ARequest_BuildsIntoADraft_ThatWaitsForReview()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                rig.Conversation.Send("Build a house");
                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Working));
                rig.Settle();

                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Reviewing));
                Assert.That(rig.World.Session.Draft, Is.SameAs(rig.Conversation.Draft));
                Assert.That(rig.Conversation.DraftSummary, Is.EqualTo("1 added"));
                Assert.That(rig.World.Session.CanUndo, Is.False, "Nothing enters the history before it is accepted.");
                Assert.That(rig.Conversation.Entries.Select(e => e.Role), Is.EqualTo(new[] { ChatRole.User, ChatRole.Activity, ChatRole.Agent }));
                Assert.That(rig.Backend.Turns[0].Prompt, Does.Contain("<editor>").And.Contain("Camera looks at").And.EndWith("Build a house"));
                Assert.That(rig.Backend.Turns[0].Instructions, Is.EqualTo("Build well."));
            }
        }

        [Test]
        public void Accept_KeepsTheBuildAsOneUndoStep_NamedAfterTheRequest()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();

                rig.Conversation.Accept();

                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Idle));
                Assert.That(rig.World.Session.Draft, Is.Null);
                Assert.That(rig.World.Session.UndoDescription, Is.EqualTo("Agent: Build a house"));
                Assert.That(rig.Placed, Is.EqualTo(1));
            }
        }

        [Test]
        public void Discard_TakesTheBuildAway()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();

                rig.Conversation.Discard();

                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Idle));
                Assert.That(rig.Placed, Is.Zero);
                Assert.That(rig.World.Session.CanUndo, Is.False);
            }
        }

        [Test]
        public void AnAnswer_NeedsNoReview()
        {
            using (var rig = new Rig((turn, report, cancellation, tools) =>
                   {
                       report(AgentEvent.Said("There are three kinds of objects."));
                       return Task.CompletedTask;
                   }))
            {
                rig.Conversation.Send("What can you place?");
                rig.Settle();

                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Idle));
                Assert.That(rig.World.Session.Draft, Is.Null);
            }
        }

        [Test]
        public void AFollowUp_ContinuesTheConversation_AndRefinesTheSameDraft()
        {
            var x = 10f;
            using (var rig = new Rig((turn, report, cancellation, tools) => BuildsAHouse(x += 20f)(turn, report, cancellation, tools)))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();
                var draft = rig.Conversation.Draft;

                rig.Conversation.Send("And another one");
                rig.Settle();

                Assert.That(rig.Conversation.Draft, Is.SameAs(draft));
                Assert.That(rig.Conversation.DraftSummary, Is.EqualTo("2 added"));
                Assert.That(rig.Backend.Turns[0].ConversationId, Is.Null);
                Assert.That(rig.Backend.Turns[1].ConversationId, Is.EqualTo("c1"));

                rig.Conversation.Accept();
                Assert.That(rig.World.Session.UndoDescription, Is.EqualTo("Agent: Build a house"), "The draft is named after the request that started it.");
            }
        }

        [Test]
        public void Retry_DiscardsTheBuild_AndAsksAgain()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();

                rig.Conversation.Retry();
                rig.Settle();

                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Reviewing));
                Assert.That(rig.Placed, Is.EqualTo(1), "The first house is gone; the new one stands.");
                Assert.That(rig.Backend.Turns[1].Prompt, Does.Contain("discarded").And.Contain("Build a house"));
                Assert.That(rig.Backend.Turns[1].ConversationId, Is.EqualTo("c1"));
            }
        }

        [Test]
        public void Stop_KeepsWhatWasBuiltForReview()
        {
            using (var rig = new Rig(async (turn, report, cancellation, tools) =>
                   {
                       await tools.CallAsync("place_objects", House(20f));
                       await Task.Delay(Timeout.Infinite, cancellation);
                   }))
            {
                rig.Conversation.Send("Build a village");
                rig.PumpUntil(() => rig.Placed == 1);

                rig.Conversation.Stop();
                rig.Settle();

                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Reviewing));
                Assert.That(rig.Conversation.Entries.Last().Text, Is.EqualTo("Stopped."));
            }
        }

        [Test]
        public void AFailedRequest_SaysWhy()
        {
            using (var rig = new Rig((turn, report, cancellation, tools) =>
                   {
                       report(AgentEvent.Failure("Claude Code stopped: overloaded"));
                       return Task.CompletedTask;
                   }))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();

                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Idle));
                Assert.That(rig.Conversation.Entries.Last().Role, Is.EqualTo(ChatRole.Error));
                Assert.That(rig.Conversation.Entries.Last().Text, Does.Contain("overloaded"));
            }
        }

        [Test]
        public void ACrashingBackend_IsReported_NotThrown()
        {
            using (var rig = new Rig((turn, report, cancellation, tools) => throw new InvalidOperationException("boom")))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();

                Assert.That(rig.Conversation.State, Is.EqualTo(AgentState.Idle));
                Assert.That(rig.Conversation.Entries.Last().Text, Does.Contain("boom"));
            }
        }

        [Test]
        public void StrayToolCalls_AreRefusedBetweenRequests()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                var call = rig.Tools.CallAsync("place_objects", House(20f));
                rig.PumpUntil(() => call.IsCompleted);

                Assert.That(call.Result.IsError, Is.True);
                Assert.That(rig.Placed, Is.Zero);
            }
        }

        [Test]
        public void Restart_StartsAFreshConversation()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();
                rig.Conversation.Accept();

                rig.Conversation.Restart();
                rig.Conversation.Send("Build another");
                rig.Settle();

                Assert.That(rig.Conversation.Entries.First().Text, Is.EqualTo("Build another"));
                Assert.That(rig.Backend.Turns[1].ConversationId, Is.Null);
            }
        }

        [Test]
        public void Dispose_StopsTheBackend()
        {
            var rig = new Rig(BuildsAHouse());

            rig.Dispose();

            Assert.That(rig.Backend.Disposed, Is.True);
        }
    }
}
