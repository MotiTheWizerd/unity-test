using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Forge.Agent.Memory;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Forge.Tests.Agent
{
    public class SemantixMemoryTests
    {
        const string Agents = "/api/v1/memory/agents";

        sealed class Request
        {
            public Request(string method, string path, JObject body)
            {
                Method = method;
                Path = path;
                Body = body;
            }

            public string Method { get; }

            public string Path { get; }

            public JObject Body { get; }
        }

        /// <summary>The server, as a function of the request; records every request it is sent.</summary>
        sealed class FakeHttp : ISemantixHttp
        {
            readonly Func<Request, JToken> _answer;

            public FakeHttp(Func<Request, JToken> answer) => _answer = answer;

            public List<Request> Requests { get; } = new List<Request>();

            public Task<JToken> GetAsync(string path, TimeSpan timeout, CancellationToken cancellation) => Send(new Request("GET", path, null));

            public Task<JToken> PostAsync(string path, JObject body, TimeSpan timeout, CancellationToken cancellation) =>
                Send(new Request("POST", path, (JObject)body.DeepClone()));

            Task<JToken> Send(Request request)
            {
                Requests.Add(request);
                return Task.FromResult(_answer(request));
            }
        }

        sealed class FakeModel : IMemoryModel
        {
            public List<(string Instruction, string Message)> Asked { get; } = new List<(string, string)>();

            public Task<string> AnswerAsync(string instruction, string message, CancellationToken cancellation)
            {
                Asked.Add((instruction, message));
                return Task.FromResult("{\"memories\":[]}");
            }
        }

        static JArray Roster(params (string Name, string Id)[] agents) =>
            new JArray(agents.Select(agent => new JObject { ["name"] = agent.Name, ["agent_id"] = agent.Id }));

        static JToken Dressed(Request request) =>
            request.Path.EndsWith("/dress") ? new JObject { ["block"] = "<agent-memory>…</agent-memory>", ["recalled"] = new JArray("likes-stone") } : null;

        static IReadOnlyList<MemoryCall> OneCall() => new[] { new MemoryCall("Build a house", "Built one.", new[] { "← place_objects: Placed 1 object" }) };

        [Test]
        public void Dress_FindsTheMemoryByName_AndRecallsOnTheCue()
        {
            var http = new FakeHttp(request => request.Method == "GET" ? Roster(("Brokkr", "b1"), ("Forge", "f1")) : Dressed(request));
            var memory = new SemantixMemory(http, new FakeModel(), "Forge");

            var dress = memory.DressAsync("  Build a house  ", CancellationToken.None).Result;
            memory.DressAsync("And a fence", CancellationToken.None).Wait();

            Assert.That(dress.Block, Is.EqualTo("<agent-memory>…</agent-memory>"));
            Assert.That(dress.Recalled, Is.EqualTo(new[] { "likes-stone" }));
            Assert.That(http.Requests.Select(r => $"{r.Method} {r.Path}"), Is.EqualTo(new[]
            {
                $"GET {Agents}", $"POST {Agents}/f1/dress", $"POST {Agents}/f1/dress",
            }), "The memory is looked up once.");
            Assert.That((string)http.Requests[1].Body["query"], Is.EqualTo("Build a house"));
        }

        [Test]
        public void Dress_MakesTheMemory_WhenTheAccountHasNone()
        {
            var http = new FakeHttp(request =>
                request.Method == "GET" ? new JArray()
                : request.Path == Agents ? new JObject { ["agent_id"] = "new1", ["name"] = "Forge" }
                : Dressed(request));
            var memory = new SemantixMemory(http, new FakeModel(), "Forge");

            memory.DressAsync("Build a house", CancellationToken.None).Wait();

            var made = http.Requests[1].Body;
            Assert.That((string)made["name"], Is.EqualTo("Forge"));
            Assert.That((string)made["origin"], Is.EqualTo("user"));
            Assert.That((bool)made["memory_enabled"], Is.True);
            Assert.That(http.Requests[2].Path, Is.EqualTo($"{Agents}/new1/dress"));
        }

        [Test]
        public void Dress_CutsALongCue_ToWhatRecallReads()
        {
            var http = new FakeHttp(request => request.Method == "GET" ? Roster(("Forge", "f1")) : Dressed(request));
            var memory = new SemantixMemory(http, new FakeModel(), "Forge");

            memory.DressAsync(new string('a', SemantixMemory.CueLength + 500), CancellationToken.None).Wait();

            Assert.That(((string)http.Requests[1].Body["query"]).Length, Is.EqualTo(SemantixMemory.CueLength));
        }

        [Test]
        public void Dress_LooksTheMemoryUpAgain_AfterItWasDeleted()
        {
            var deleted = true;
            var http = new FakeHttp(request =>
            {
                if (request.Method == "GET")
                    return Roster(("Forge", deleted ? "old" : "f2"));
                if (request.Path == $"{Agents}/old/dress")
                {
                    deleted = false;
                    throw new SemantixException("not one of your agents", 404);
                }

                return Dressed(request);
            });
            var memory = new SemantixMemory(http, new FakeModel(), "Forge");

            var failure = Assert.Throws<AggregateException>(() => memory.DressAsync("Build", CancellationToken.None).Wait());
            Assert.That(failure.InnerException, Is.TypeOf<SemantixException>());
            memory.DressAsync("Build", CancellationToken.None).Wait();

            Assert.That(http.Requests.Last().Path, Is.EqualTo($"{Agents}/f2/dress"));
        }

        [Test]
        public void Sleep_DistillsOnTheModel_AndTheServerKeepsTheAnswer()
        {
            var http = new FakeHttp(request =>
                request.Method == "GET" ? Roster(("Forge", "f1"))
                : request.Path.EndsWith("/sleep/prompt") ? new JObject { ["agent"] = "f1", ["instruction"] = "You distill.", ["message"] = "The run." }
                : new JObject { ["agent"] = "f1", ["created"] = 1, ["updated"] = 1, ["names"] = new JArray("likes-stone", "village") });
            var model = new FakeModel();
            var memory = new SemantixMemory(http, model, "Forge");

            var sleep = memory.SleepAsync(OneCall(), new[] { "likes-stone", "likes-stone" }, "forge-1", CancellationToken.None).Result;

            Assert.That(model.Asked.Single(), Is.EqualTo(("You distill.", "The run.")));
            var prompt = http.Requests[1].Body;
            var reply = http.Requests[2].Body;
            Assert.That(http.Requests[1].Path, Is.EqualTo($"{Agents}/f1/sleep/prompt"));
            Assert.That(http.Requests[2].Path, Is.EqualTo($"{Agents}/f1/sleep/reply"));
            Assert.That((string)prompt["calls"][0]["prompt"], Is.EqualTo("Build a house"));
            Assert.That((string)prompt["calls"][0]["answer"], Is.EqualTo("Built one."));
            Assert.That((string)prompt["calls"][0]["tool_trail"][0], Is.EqualTo("← place_objects: Placed 1 object"));
            Assert.That(prompt["recalled"].Select(n => (string)n), Is.EqualTo(new[] { "likes-stone" }));
            Assert.That((string)prompt["run_id"], Is.EqualTo("forge-1"));
            Assert.That((string)reply["reply"], Is.EqualTo("{\"memories\":[]}"));
            reply.Remove("reply");
            Assert.That(JToken.DeepEquals(prompt, reply), Is.True, "Both halves get the same run, so the server rebuilds the pass.");
            Assert.That((sleep.Created, sleep.Updated), Is.EqualTo((1, 1)));
            Assert.That(sleep.Describe(), Is.EqualTo("Remembered 2 things."));
        }

        [Test]
        public void Sleep_WithNothingToDistill_NeverAsksTheModel()
        {
            var http = new FakeHttp(request =>
                request.Method == "GET" ? Roster(("Forge", "f1")) : new JObject { ["agent"] = "f1", ["skipped"] = "this agent keeps no memory" });
            var model = new FakeModel();
            var memory = new SemantixMemory(http, model, "Forge");

            var sleep = memory.SleepAsync(OneCall(), Array.Empty<string>(), null, CancellationToken.None).Result;

            Assert.That(model.Asked, Is.Empty);
            Assert.That(sleep.Skipped, Is.EqualTo("this agent keeps no memory"));
            Assert.That(sleep.Describe(), Is.Null, "A skip is nothing to tell the user.");
            Assert.That(http.Requests[1].Body["run_id"], Is.Null);
        }

        [Test]
        public void Sleep_Failures_ComeBackInTheResult()
        {
            var http = new FakeHttp(request => throw new SemantixException("couldn't reach Semantix"));
            var memory = new SemantixMemory(http, new FakeModel(), "Forge");

            var sleep = memory.SleepAsync(OneCall(), null, "forge-1", CancellationToken.None).Result;

            Assert.That(sleep.Error, Is.EqualTo("couldn't reach Semantix"));
            Assert.That(sleep.Describe(), Is.EqualTo("Memory couldn't keep this: couldn't reach Semantix"));
        }

        [Test]
        public void Credentials_AreTheSemantixLogin_OrSayHowToSignIn()
        {
            var folder = Path.Combine(Path.GetTempPath(), "forge-credentials-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var file = Path.Combine(folder, "credentials.json");
                Assert.That(Assert.Throws<SemantixException>(() => SemantixCredentials.ReadToken(file)).Message, Does.Contain("sign in"));

                File.WriteAllText(file, "{\"token\":\"sxa_abc\",\"token_id\":\"t\",\"user_id\":\"u\"}");
                Assert.That(SemantixCredentials.ReadToken(file), Is.EqualTo("sxa_abc"));

                File.WriteAllText(file, "not json");
                Assert.That(Assert.Throws<SemantixException>(() => SemantixCredentials.ReadToken(file)).Message, Does.Contain("damaged"));

                File.WriteAllText(file, "{\"user_id\":\"u\"}");
                Assert.That(Assert.Throws<SemantixException>(() => SemantixCredentials.ReadToken(file)).Message, Does.Contain("no token"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
