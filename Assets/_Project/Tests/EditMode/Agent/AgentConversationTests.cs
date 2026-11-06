using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Forge.Agent;
using Forge.Agent.Memory;
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

        sealed class Sleep
        {
            public Sleep(IReadOnlyList<MemoryCall> calls, IReadOnlyList<string> recalled, string runId)
            {
                Calls = calls;
                Recalled = recalled;
                RunId = runId;
            }

            public IReadOnlyList<MemoryCall> Calls { get; }

            public IReadOnlyList<string> Recalled { get; }

            public string RunId { get; }
        }

        /// <summary>A memory that dresses as told and records what it is asked; called from worker threads.</summary>
        sealed class FakeMemory : IAgentMemory
        {
            readonly List<string> _cues = new List<string>();
            readonly List<Sleep> _sleeps = new List<Sleep>();

            public Func<string, MemoryDress> Dress { get; set; } = cue => MemoryDress.None;

            public MemorySleep Kept { get; set; } = MemorySleep.Skip("nothing new");

            public IReadOnlyList<string> Cues
            {
                get
                {
                    lock (_cues)
                        return _cues.ToList();
                }
            }

            public IReadOnlyList<Sleep> Sleeps
            {
                get
                {
                    lock (_sleeps)
                        return _sleeps.ToList();
                }
            }

            public Task<MemoryDress> DressAsync(string cue, CancellationToken cancellation)
            {
                lock (_cues)
                    _cues.Add(cue);
                return Task.FromResult(Dress(cue));
            }

            public Task<MemorySleep> SleepAsync(IReadOnlyList<MemoryCall> calls, IReadOnlyList<string> recalled, string runId, CancellationToken cancellation)
            {
                lock (_sleeps)
                    _sleeps.Add(new Sleep(calls, recalled, runId));
                return Task.FromResult(Kept);
            }
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
                Memory = new FakeMemory();
                Conversation = new AgentConversation(Backend, Tools, World.Session, Queue, () => "Camera looks at (20, 20).", "Build well.", Memory);
            }

            public BuildingFixture World { get; }

            public MainThreadQueue Queue { get; }

            public AgentToolHost Tools { get; }

            public FakeBackend Backend { get; }

            public FakeMemory Memory { get; }

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

        static Behaviour Answers(string answer) => (turn, report, cancellation, tools) =>
        {
            report(AgentEvent.Said(answer));
            report(AgentEvent.Done());
            return Task.CompletedTask;
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

        [Test]
        public void ATurn_WearsWhatTheMemoryRecalls_InFrontOfTheBriefing()
        {
            using (var rig = new Rig(Answers("Stone, as you like it.")))
            {
                rig.Memory.Dress = cue => new MemoryDress("<agent-memory>\nThe user likes stone.\n</agent-memory>\n\n", new[] { "likes-stone" });

                rig.Conversation.Send("Build a house");
                rig.Settle();

                var prompt = rig.Backend.Turns[0].Prompt;
                Assert.That(prompt, Does.StartWith("<agent-memory>\nThe user likes stone.\n</agent-memory>\n\n<editor>").And.EndWith("Build a house"));
                Assert.That(rig.Memory.Cues, Is.EqualTo(new[] { "Build a house" }), "Recall is cued by the message, not the briefing.");
                Assert.That(rig.Backend.Turns[0].Instructions, Is.EqualTo("Build well."), "The system prompt stays the same, so a resumed conversation keeps its cache.");
            }
        }

        [Test]
        public void AnUnreachableMemory_IsSaidOnce_AndTheAgentWorksWithoutIt()
        {
            using (var rig = new Rig(Answers("Done.")))
            {
                rig.Memory.Dress = cue => throw new SemantixException("not signed in to Semantix");

                rig.Conversation.Send("Build a house");
                rig.Settle();
                rig.Conversation.Send("And a fence");
                rig.Settle();

                Assert.That(rig.Backend.Turns.Count, Is.EqualTo(2));
                Assert.That(rig.Backend.Turns[0].Prompt, Does.StartWith("<editor>"));
                Assert.That(rig.Conversation.Entries.Count(e => e.Role == ChatRole.Notice && e.Text.Contains("Working without memory: not signed in")), Is.EqualTo(1));
            }
        }

        [Test]
        public void AnAnswer_IsSleptAtOnce_AsTheMessageAndWhatWasSaid()
        {
            using (var rig = new Rig(Answers("Three kinds.")))
            {
                rig.Conversation.Send("What can you place?");
                rig.Settle();
                rig.PumpUntil(() => rig.Memory.Sleeps.Count == 1);

                var call = rig.Memory.Sleeps[0].Calls.Single();
                Assert.That(call.Prompt, Is.EqualTo("What can you place?"), "The briefing is the editor's state, not something to remember.");
                Assert.That(call.Answer, Is.EqualTo("Three kinds."));
                Assert.That(call.ToolTrail, Is.Empty);
                Assert.That(rig.Memory.Sleeps[0].RunId, Does.StartWith("forge-"));
            }
        }

        [Test]
        public void ABuild_IsSleptWhenAccepted_WithEveryTurnAndTheVerdict()
        {
            var x = 10f;
            using (var rig = new Rig((turn, report, cancellation, tools) => BuildsAHouse(x += 20f)(turn, report, cancellation, tools)))
            {
                var recalls = new Queue<string[]>(new[] { new[] { "a" }, new[] { "a", "b" } });
                rig.Memory.Dress = cue => new MemoryDress("<agent-memory>…</agent-memory>", recalls.Dequeue());

                rig.Conversation.Send("Build a house");
                rig.Settle();
                rig.Conversation.Send("And another one");
                rig.Settle();
                Assert.That(rig.Memory.Sleeps, Is.Empty, "A build waits for the user's verdict.");

                rig.Conversation.Accept();
                rig.PumpUntil(() => rig.Memory.Sleeps.Count == 1);

                var sleep = rig.Memory.Sleeps[0];
                Assert.That(sleep.Calls.Select(c => c.Prompt), Is.EqualTo(new[] { "Build a house", "And another one", "I accepted what you built (2 added) and kept it." }));
                Assert.That(sleep.Calls[0].Answer, Is.EqualTo("Built a house."));
                Assert.That(sleep.Calls[0].ToolTrail.Single(), Does.StartWith("← place_objects: "));
                Assert.That(sleep.Recalled, Is.EqualTo(new[] { "a", "b" }), "Each recalled memory once, so the sleep doesn't write it again.");
            }
        }

        [Test]
        public void ADiscardedBuild_IsSleptAsDiscarded()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();

                rig.Conversation.Discard();
                rig.PumpUntil(() => rig.Memory.Sleeps.Count == 1);

                Assert.That(rig.Memory.Sleeps[0].Calls.Last().Prompt, Is.EqualTo("I discarded what you built."));
            }
        }

        [Test]
        public void ARetry_StaysInTheSameRequest_UntilItSettles()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();
                rig.Conversation.Retry();
                rig.Settle();
                rig.Conversation.Accept();
                rig.PumpUntil(() => rig.Memory.Sleeps.Count == 1);

                var prompts = rig.Memory.Sleeps[0].Calls.Select(c => c.Prompt).ToList();
                Assert.That(prompts.Count, Is.EqualTo(3));
                Assert.That(prompts[1], Does.StartWith("I discarded what you built. Build it again"));
                Assert.That(prompts[2], Does.StartWith("I accepted"));
            }
        }

        [Test]
        public void WhatTheMemoryKept_IsSaidInTheChat()
        {
            using (var rig = new Rig(Answers("Noted: you like stone.")))
            {
                rig.Memory.Kept = new MemorySleep(1, 1, new[] { "likes-stone", "village-plan" }, null, null);

                rig.Conversation.Send("I like stone houses");
                rig.Settle();
                rig.PumpUntil(() => rig.Conversation.Entries.Last().Text == "Remembered 2 things.");
            }
        }

        static readonly AgentImage Picture = new AgentImage("image/png", new byte[] { 1, 2, 3 });

        [Test]
        public void Pictures_GoToTheAgent_AndStayOnTheUsersLine()
        {
            using (var rig = new Rig(Answers("A stone tower.")))
            {
                rig.Conversation.Send("Build this", new[] { Picture });
                rig.Settle();

                Assert.That(rig.Backend.Turns[0].Images, Is.EqualTo(new[] { Picture }));
                Assert.That(rig.Conversation.Entries[0].Images, Is.EqualTo(new[] { Picture }));
                Assert.That(rig.Conversation.Entries[1].Images, Is.Empty);
            }
        }

        [Test]
        public void APictureAlone_IsAMessage()
        {
            using (var rig = new Rig(Answers("A stone tower.")))
            {
                rig.Conversation.Send("  ", new[] { Picture });
                rig.Settle();

                Assert.That(rig.Conversation.Entries[0].Text, Is.Empty);
                Assert.That(rig.Backend.Turns[0].Prompt, Does.EndWith("Look at the picture."));
            }

            using (var rig = new Rig(Answers("?")))
                Assert.Throws<ArgumentException>(() => rig.Conversation.Send("  ", AgentImage.None));
        }

        [Test]
        public void TheMemory_IsToldAPictureWasThere()
        {
            using (var rig = new Rig(Answers("A stone tower.")))
            {
                rig.Conversation.Send("What is this?", new[] { Picture, Picture });
                rig.Settle();
                rig.PumpUntil(() => rig.Memory.Sleeps.Count == 1);

                Assert.That(rig.Memory.Sleeps[0].Calls[0].Prompt, Is.EqualTo("What is this?\n(2 pictures attached)"));
            }
        }

        [Test]
        public void ARetry_DoesNotSendThePicturesAgain()
        {
            using (var rig = new Rig(BuildsAHouse()))
            {
                rig.Conversation.Send("Build this", new[] { Picture });
                rig.Settle();
                rig.Conversation.Retry();
                rig.Settle();

                Assert.That(rig.Backend.Turns[1].Images, Is.Empty, "The resumed conversation already holds them.");
            }
        }

        [Test]
        public void AFailureBeforeAnyWord_IsNotSlept()
        {
            using (var rig = new Rig((turn, report, cancellation, tools) =>
                   {
                       report(AgentEvent.Failure("Claude Code stopped: overloaded"));
                       return Task.CompletedTask;
                   }))
            {
                rig.Conversation.Send("Build a house");
                rig.Settle();
                var clock = Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < 100)
                {
                    rig.Queue.Pump();
                    Thread.Sleep(1);
                }

                Assert.That(rig.Memory.Sleeps, Is.Empty);
            }
        }
    }
}
