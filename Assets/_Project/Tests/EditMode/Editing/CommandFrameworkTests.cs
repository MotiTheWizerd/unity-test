using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class CommandFrameworkTests
    {
        sealed class EchoCommand : EditorCommand<EchoCommand.Arguments, string>
        {
            public EchoCommand(string name = "echo") : base(name, "Says the text back.")
            {
            }

            public sealed class Arguments
            {
                [Param("What to say.")]
                public string Text { get; set; }

                [Param("How loud, 1 to 3.", Optional = true, Minimum = 1, Maximum = 3)]
                public int? Volume { get; set; }

                [Param("Extra words.", Optional = true, MaxItems = 2)]
                public List<Word> Words { get; set; }
            }

            public sealed class Word
            {
                [Param("A word.")]
                public string Text { get; set; }
            }

            protected override string Run(Arguments arguments) => arguments.Text;
        }

        sealed class Undescribed
        {
            public string Text { get; set; }
        }

        sealed class OptionalValue
        {
            [Param("A number that can't be left out, though marked optional.", Optional = true)]
            public float Number { get; set; }
        }

        sealed class RangedText
        {
            [Param("Text with a range.", Minimum = 1)]
            public string Text { get; set; }
        }

        [Test]
        public void Registry_FindsCommandsByName_AndRejectsDuplicates()
        {
            var registry = new CommandRegistry();
            registry.Add(new EchoCommand());

            Assert.That(registry.TryGet("echo", out var found), Is.True);
            Assert.That(found.Name, Is.EqualTo("echo"));
            Assert.That(registry.TryGet("missing", out _), Is.False);
            Assert.Throws<ArgumentException>(() => registry.Add(new EchoCommand()));
        }

        [TestCase("Echo")]
        [TestCase("say hello")]
        [TestCase("1st")]
        [TestCase("")]
        public void CommandNames_MustBeSnakeCase(string name)
        {
            Assert.Throws<ArgumentException>(() => new EchoCommand(name));
        }

        [Test]
        public void Execute_ChecksTheArgumentsFirst()
        {
            var command = new EchoCommand();

            Assert.That(command.Execute(new EchoCommand.Arguments { Text = "hi", Volume = 2 }), Is.EqualTo("hi"));

            var missing = Assert.Throws<CommandException>(() => command.Execute(new EchoCommand.Arguments()));
            Assert.That(missing.Message, Does.Contain("'text' is required"));

            var loud = Assert.Throws<CommandException>(() => command.Execute(new EchoCommand.Arguments { Text = "hi", Volume = 9 }));
            Assert.That(loud.Message, Does.Contain("'volume' must be at most 3"));
        }

        [Test]
        public void Validation_NamesTheBrokenItemInALists()
        {
            var command = new EchoCommand();
            var arguments = new EchoCommand.Arguments
            {
                Text = "hi",
                Words = new List<EchoCommand.Word> { new EchoCommand.Word { Text = "a" }, new EchoCommand.Word { Text = " " } },
            };

            var error = Assert.Throws<CommandException>(() => command.Execute(arguments));
            Assert.That(error.Message, Does.Contain("'words[1].text' must not be empty"));

            arguments.Words.Add(new EchoCommand.Word { Text = "c" });
            error = Assert.Throws<CommandException>(() => command.Execute(arguments));
            Assert.That(error.Message, Does.Contain("'words' takes at most 2"));
        }

        [Test]
        public void Validation_RejectsNonFiniteNumbers()
        {
            var command = new PlaceObjectsCommand(BuildingFixture.Create().Context);
            var arguments = new PlaceObjectsCommand.Arguments
            {
                Objects = new List<PlaceObjectsCommand.Item> { new PlaceObjectsCommand.Item { Object = "core:house", X = float.NaN, Z = 1f } },
            };

            var error = Assert.Throws<CommandException>(() => command.Execute(arguments));
            Assert.That(error.Message, Does.Contain("'objects[0].x' must be a finite number"));
        }

        [TestCase("RandomGap", "random_gap")]
        [TestCase("X", "x")]
        [TestCase("CenterX", "center_x")]
        [TestCase("Objects", "objects")]
        [TestCase("MaxHTTPCount", "max_http_count")]
        public void ParameterNames_AreSnakeCase(string property, string expected)
        {
            Assert.That(CommandParameters.NameOf(property), Is.EqualTo(expected));
        }

        [Test]
        public void Parameters_ReadKindsOptionalityAndOrder()
        {
            var parameters = CommandParameters.Of(typeof(EchoCommand.Arguments));

            Assert.That(parameters.Select(p => p.Name), Is.EqualTo(new[] { "text", "volume", "words" }));
            Assert.That(parameters[0].Kind, Is.EqualTo(ParameterKind.String));
            Assert.That(parameters[1].Kind, Is.EqualTo(ParameterKind.Integer));
            Assert.That(parameters[1].Info.Optional, Is.True);
            Assert.That(parameters[2].Kind, Is.EqualTo(ParameterKind.List));
            Assert.That(parameters[2].ItemKind, Is.EqualTo(ParameterKind.Object));
            Assert.That(parameters[2].ItemType, Is.EqualTo(typeof(EchoCommand.Word)));
        }

        [TestCase(typeof(Undescribed))]
        [TestCase(typeof(OptionalValue))]
        [TestCase(typeof(RangedText))]
        public void MalformedArgumentClasses_FailLoudly(Type arguments)
        {
            Assert.Throws<InvalidOperationException>(() => CommandParameters.Of(arguments));
        }

        [Test]
        public void EveryBuildingCommand_HasWellFormedArguments()
        {
            var registry = BuildingFixture.Create().Registry;

            Assert.That(registry.Commands.Select(c => c.Name),
                Is.EqualTo(new[] { "list_objects", "inspect_area", "place_objects", "place_line", "delete_objects" }));
            foreach (var command in registry.Commands)
                Assert.DoesNotThrow(() => Walk(command.ArgumentsType), command.Name);
        }

        static void Walk(Type arguments)
        {
            foreach (var parameter in CommandParameters.Of(arguments))
            {
                if (parameter.Kind == ParameterKind.Object)
                    Walk(parameter.ValueType);
                if (parameter.ItemKind == ParameterKind.Object)
                    Walk(parameter.ItemType);
            }
        }
    }
}
