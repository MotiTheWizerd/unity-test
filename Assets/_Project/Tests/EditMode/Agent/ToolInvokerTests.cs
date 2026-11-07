using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Agent;
using Forge.Agent.Tools;
using Forge.Editing.Building;
using Forge.Tests.Editing;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Forge.Tests.Agent
{
    public class ToolInvokerTests
    {
        static ToolInvoker NewInvoker() => new ToolInvoker(BuildingFixture.Create().Registry);

        [Test]
        public void Tools_DescribeEveryCommand_WithTheSchemaItsArgumentsAreCheckedAgainst()
        {
            var invoker = NewInvoker();

            Assert.That(invoker.Tools.Select(t => t.Name),
                Is.EqualTo(new[] { "list_objects", "inspect_area", "ground_at", "flatten_area", "sculpt_hill", "sculpt_ridge", "smooth_ground", "extend_land", "set_sea_level", "set_climate", "add_lake", "set_lake_level", "remove_lake", "place_objects", "place_line", "place_outline", "scatter_objects", "adjust_objects", "list_materials", "set_materials", "list_grounds", "paint_line", "paint_area", "auto_paint", "delete_objects", "undo_last" }));

            var schema = invoker.Tools.Single(t => t.Name == "place_objects").InputSchema;
            Assert.That((string)schema["type"], Is.EqualTo("object"));
            Assert.That(schema["required"].Values<string>(), Is.EqualTo(new[] { "objects" }));
            var objects = schema["properties"]["objects"];
            Assert.That((string)objects["type"], Is.EqualTo("array"));
            Assert.That((int)objects["minItems"], Is.EqualTo(1));
            Assert.That((int)objects["maxItems"], Is.EqualTo(PlaceObjectsCommand.MaxObjects));

            var item = objects["items"];
            Assert.That(item["required"].Values<string>(), Is.EqualTo(new[] { "object", "x", "z" }));
            Assert.That((bool)item["additionalProperties"], Is.False);
            Assert.That((double)item["properties"]["scale"]["minimum"], Is.EqualTo(0.1).Within(1e-9));
            Assert.That((string)item["properties"]["heading"]["description"], Does.Contain("Compass"));
        }

        [Test]
        public void Invoke_ReadsSnakeCaseArguments_AndAnswersInSnakeCase()
        {
            var result = NewInvoker().Invoke("place_line", JObject.Parse(
                "{\"object\":\"core:fence\",\"points\":[{\"x\":10,\"z\":10},{\"x\":22,\"z\":10}],\"gap\":1,\"random_gap\":0}"));

            Assert.That(result.IsError, Is.False, result.Content);
            var answer = JObject.Parse(result.Content);
            Assert.That((string)answer["summary"], Does.StartWith("Placed 4 Wooden Fence"));
            Assert.That(answer["ids"].Count(), Is.EqualTo(4));
            Assert.That(result.Summary, Is.EqualTo((string)answer["summary"]));
        }

        /// <summary>A command that brings a picture back, as the editor's look does.</summary>
        internal sealed class Snapshot : Forge.Editing.Commands.EditorCommand<Snapshot.Arguments, Snapshot.Result>
        {
            public Snapshot() : base("snapshot", "Takes a picture.")
            {
            }

            public sealed class Arguments
            {
            }

            public sealed class Result : IPictureResult
            {
                public string Summary => "Took a picture.";

                public int Width => 2;

                public IReadOnlyList<AgentImage> Pictures { get; } = new[] { new AgentImage("image/png", new byte[] { 9 }) };
            }

            protected override Result Run(Arguments arguments) => new Result();
        }

        [Test]
        public void Invoke_APictureResult_BringsItsPictures_OutsideTheJson()
        {
            var registry = new Forge.Editing.Commands.CommandRegistry();
            registry.Add(new Snapshot());

            var result = new ToolInvoker(registry).Invoke("snapshot", new JObject());

            Assert.That(result.IsError, Is.False, result.Content);
            Assert.That(result.Pictures.Single().Bytes, Is.EqualTo(new byte[] { 9 }));
            var answer = JObject.Parse(result.Content);
            Assert.That(answer.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "summary", "width" }));
        }

        [Test]
        public void Invoke_AMissingArgument_IsAnErrorTheModelCanRead()
        {
            var result = NewInvoker().Invoke("inspect_area", JObject.Parse("{\"x\":5,\"size\":10}"));

            Assert.That(result.IsError, Is.True);
            Assert.That(result.Content, Does.Contain("'z'"));
        }

        [Test]
        public void Invoke_AnUnknownArgument_IsAnError()
        {
            var result = NewInvoker().Invoke("list_objects", JObject.Parse("{\"filter\":\"tree\"}"));

            Assert.That(result.IsError, Is.True);
            Assert.That(result.Content, Does.Contain("filter"));
        }

        [Test]
        public void Invoke_ABrokenRule_NamesTheArgument()
        {
            var result = NewInvoker().Invoke("place_objects", JObject.Parse(
                "{\"objects\":[{\"object\":\"core:house\",\"x\":5,\"z\":5,\"scale\":50}]}"));

            Assert.That(result.IsError, Is.True);
            Assert.That(result.Content, Does.Contain("objects[0].scale"));
        }

        [Test]
        public void Invoke_ARefusal_ReturnsItsReason()
        {
            var result = NewInvoker().Invoke("place_objects", JObject.Parse(
                "{\"objects\":[{\"object\":\"core:dragon\",\"x\":5,\"z\":5}]}"));

            Assert.That(result.IsError, Is.True);
            Assert.That(result.Content, Does.Contain("core:dragon"));
        }

        [Test]
        public void Invoke_AnUnknownTool_IsAnError()
        {
            Assert.That(NewInvoker().Invoke("summon_dragon", new JObject()).IsError, Is.True);
        }

        [Test]
        public void ToolHost_RunsCallsWhenTheMainThreadPumps_AndOnlyWhileOpen()
        {
            var queue = new MainThreadQueue();
            var host = new AgentToolHost(NewInvoker(), queue);
            var calls = new List<ToolCall>();
            host.Called += calls.Add;

            var refused = host.CallAsync("list_objects", new JObject());
            Assert.That(refused.IsCompleted, Is.False, "Nothing runs until the main thread pumps.");
            queue.Pump();
            Assert.That(refused.Result.IsError, Is.True, "Closed between requests.");

            host.Open();
            var listed = host.CallAsync("list_objects", new JObject());
            queue.Pump();
            Assert.That(listed.Result.IsError, Is.False);
            Assert.That(calls.Select(c => c.Tool), Is.EqualTo(new[] { "list_objects", "list_objects" }));
        }

        [Test]
        public void MainThreadQueue_CarriesResultsAndErrors_AndRunsLaterWorkOnTheNextPump()
        {
            var queue = new MainThreadQueue();
            var order = new List<string>();

            var answer = queue.Run(() => 42);
            var failure = queue.Run<int>(() => throw new InvalidOperationException("no"));
            queue.Post(() =>
            {
                order.Add("first");
                queue.Post(() => order.Add("second"));
            });
            queue.Pump();

            Assert.That(answer.Result, Is.EqualTo(42));
            Assert.That(failure.Exception.GetBaseException().Message, Is.EqualTo("no"));
            Assert.That(order, Is.EqualTo(new[] { "first" }));
            queue.Pump();
            Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
        }
    }
}
