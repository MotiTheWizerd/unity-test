using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Forge.Agent.Guides;
using Forge.Agent.Tools;
using Forge.Editing.Commands;
using Forge.Tests.Editing;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Agent
{
    public class AgentGuidesTests
    {
        const string Towns = "---\nwhen: before laying out a town\n---\n# Towns\n\nStreets 6 to 8 m wide.\n";

        // The system prompt goes on Claude Code's command line, which Windows caps at 32,767 characters in all.
        const int MaxSystemPrompt = 24000;

        string _folder;

        [SetUp]
        public void SetUp() => _folder = Path.Combine(Path.GetTempPath(), "forge-guides-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [Test]
        public void Parse_ReadsWhenFromTheFrontMatter_AndTheGuideAfterIt()
        {
            var guide = AgentGuides.Parse("towns", "---\r\nwhen:  before laying out a town \r\n---\r\n\r\n# Towns\r\n\r\nStreets 6 to 8 m wide.\r\n");

            Assert.That(guide.Topic, Is.EqualTo("towns"));
            Assert.That(guide.When, Is.EqualTo("before laying out a town"));
            Assert.That(guide.Text, Is.EqualTo("# Towns\n\nStreets 6 to 8 m wide."));
        }

        [TestCase("# Towns\n\nNo front matter.", "must open with a front matter")]
        [TestCase("---\nwhen: before a town", "no closing")]
        [TestCase("---\nwhen: before a town\n# Towns", "is the closing \"---\" missing?")]
        [TestCase("---\n---\n# Towns", "must say 'when'")]
        [TestCase("---\nwhen:\n---\n# Towns", "must say 'when'")]
        [TestCase("---\nwhen: before a town\ntitle: Towns\n---\n# Towns", "unknown front matter field 'title'")]
        [TestCase("---\nwhen: before a town\nwhen: again\n---\n# Towns", "'when' twice")]
        [TestCase("---\nwhen before a town\n---\n# Towns", "not \"field: value\"")]
        [TestCase("---\nwhen: before a town\n---\n\n", "no text")]
        public void Parse_RefusesAMalformedGuide_SayingWhy(string content, string reason)
        {
            var refusal = Assert.Throws<InvalidDataException>(() => AgentGuides.Parse("towns", content));

            Assert.That(refusal.Message, Does.Contain(reason));
        }

        [Test]
        public void Read_TakesEveryMarkdownFile_AsTheGuideOnItsName()
        {
            Write("towns.md", Towns);
            Write("terrain.md", "---\nwhen: before shaping land\n---\n# Terrain\n");
            Write("towns.md.meta", "fileFormatVersion: 2");
            Write("notes.txt", "Not a guide.");

            var guides = AgentGuides.Read(_folder);

            Assert.That(guides.All.Select(g => g.Topic), Is.EqualTo(new[] { "terrain", "towns" }));
            Assert.That(guides.TryGet(" Towns ", out var towns), Is.True, "A topic is found in any case.");
            Assert.That(towns.Text, Does.StartWith("# Towns"));
            Assert.That(guides.TryGet("castles", out _), Is.False);
        }

        [Test]
        public void Read_RefusesAFileNotNamedAfterATopic()
        {
            Write("Town Planning.md", Towns);

            var refusal = Assert.Throws<InvalidDataException>(() => AgentGuides.Read(_folder));

            Assert.That(refusal.Message, Does.Contain("Town Planning.md"));
        }

        [Test]
        public void Read_AMalformedGuide_NamesItsFile()
        {
            Write("towns.md", "# Towns");

            var refusal = Assert.Throws<InvalidDataException>(() => AgentGuides.Read(_folder));

            Assert.That(refusal.Message, Does.Contain("towns.md"));
        }

        [Test]
        public void Read_AMissingFolder_Throws() =>
            Assert.Throws<DirectoryNotFoundException>(() => AgentGuides.Read(_folder));

        [Test]
        public void AppendIndex_ListsEveryGuide_WithWhenToReadIt()
        {
            var guides = new AgentGuides(new[]
            {
                new AgentGuide("towns", "before laying out a town", "# Towns"),
                new AgentGuide("terrain", "before shaping land", "# Terrain"),
            });

            var prompt = guides.AppendIndex("You build worlds.\n");

            Assert.That(prompt, Does.StartWith("You build worlds.\n\n## Guides\n\n"));
            Assert.That(prompt, Does.Contain("`read_guide`"));
            Assert.That(prompt, Does.EndWith("\n- `terrain`: before shaping land\n- `towns`: before laying out a town\n"));
        }

        [Test]
        public void AppendIndex_WithoutGuides_LeavesTheInstructionsAsTheyAre() =>
            Assert.That(AgentGuides.None.AppendIndex("You build worlds."), Is.EqualTo("You build worlds."));

        [Test]
        public void Guides_OnOneTopic_AreRefused() =>
            Assert.Throws<ArgumentException>(() => new AgentGuides(new[]
            {
                new AgentGuide("towns", "before laying out a town", "# Towns"),
                new AgentGuide("towns", "before a village", "# Villages"),
            }));

        [Test]
        public void ReadGuide_ReadsAGuideByItsTopic_OrListsThem()
        {
            var invoker = Invoker(new AgentGuide("towns", "before laying out a town", "# Towns\n\nStreets 6 to 8 m wide."));

            var read = invoker.Invoke("read_guide", JObject.Parse("{\"topic\":\"towns\"}"));
            Assert.That(read.IsError, Is.False, read.Content);
            Assert.That(read.Summary, Is.EqualTo("Read the towns guide."));
            Assert.That((string)JObject.Parse(read.Content)["text"], Is.EqualTo("# Towns\n\nStreets 6 to 8 m wide."));

            var list = JObject.Parse(invoker.Invoke("read_guide", new JObject()).Content);
            Assert.That(list["guides"].Select(g => (string)g["topic"]), Is.EqualTo(new[] { "towns" }));
            Assert.That((string)list["guides"][0]["when"], Is.EqualTo("before laying out a town"));
            Assert.That(list["text"], Is.Null, "A list carries no guide's text.");
        }

        [Test]
        public void ReadGuide_AnUnknownTopic_IsRefusedWithTheTopicsThereAre()
        {
            var result = Invoker(new AgentGuide("towns", "before laying out a town", "# Towns"))
                .Invoke("read_guide", JObject.Parse("{\"topic\":\"castles\"}"));

            Assert.That(result.IsError, Is.True);
            Assert.That(result.Content, Does.Contain("castles").And.Contain("towns"));
        }

        // What the agent is given to read (its instructions and the guides that ship) is checked against the tools
        // themselves, so renaming a tool or an argument fails here instead of leaving the agent asking for a name
        // that's gone.
        [Test]
        public void ShippedTexts_NameOnlyToolsAndArgumentsThatExist_AndFitTheCommandLine()
        {
            var guides = AgentGuides.Read(Path.Combine(Application.streamingAssetsPath, "AgentGuides"));
            Assert.That(guides.All.Select(g => g.Topic), Is.SupersetOf(new[] { "terrain", "towns" }));
            var instructions = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Settings/Agent/WorldBuilderInstructions.md"));

            var registry = BuildingFixture.Create().Registry;
            registry.Add(new ReadGuideCommand(guides));
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tool in new ToolInvoker(registry).Tools)
            {
                known.Add(tool.Name);
                ArgumentNames(tool.InputSchema, known);
            }

            var texts = guides.All.ToDictionary(g => g.Topic + AgentGuides.Extension, g => g.When + "\n" + g.Text);
            texts.Add("the instructions", instructions);
            foreach (var text in texts)
            {
                var unknown = Regex.Matches(text.Value, @"\b[a-z][a-z0-9]*(?:_[a-z0-9]+)+\b").Cast<Match>()
                    .Select(match => match.Value)
                    .Where(name => !known.Contains(name))
                    .Distinct()
                    .ToList();
                Assert.That(unknown, Is.Empty, $"{text.Key} names tools or arguments that don't exist.");
            }

            Assert.That(guides.AppendIndex(instructions).Length, Is.LessThan(MaxSystemPrompt),
                "The instructions and the guides' index are the system prompt, which goes on the command line; move detail into a guide.");
        }

        static ToolInvoker Invoker(params AgentGuide[] guides)
        {
            var registry = new CommandRegistry();
            registry.Add(new ReadGuideCommand(new AgentGuides(guides)));
            return new ToolInvoker(registry);
        }

        // Every argument's name in a tool's schema, nested ones (a list's items) too.
        static void ArgumentNames(JToken schema, ISet<string> names)
        {
            if (schema is JObject node)
            {
                if (node["properties"] is JObject properties)
                {
                    foreach (var property in properties.Properties())
                        names.Add(property.Name);
                }

                foreach (var child in node.Properties())
                    ArgumentNames(child.Value, names);
            }
            else if (schema is JArray items)
            {
                foreach (var item in items)
                    ArgumentNames(item, names);
            }
        }

        void Write(string file, string content)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, file), content);
        }
    }
}
