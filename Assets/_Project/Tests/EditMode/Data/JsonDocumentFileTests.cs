using System;
using System.Collections.Generic;
using System.IO;
using Forge.Data.Json;
using Forge.Data.Serialization;
using Forge.Editing.State;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class JsonDocumentFileTests
    {
        string _directory;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "forge-tests-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "Editor", "Worlds", "forge.user.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void Load_MissingFile_IsTheDefaultWithoutProblem()
        {
            var document = new JsonDocumentFile<EditorPrefs>(_path).Load(out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(document.FlySpeed, Is.Null);
        }

        [Test]
        public void SaveThenLoad_RoundTripsAndKeepsRecordIdKeysAsWritten()
        {
            var world = new WorldViewState
            {
                Space = "forge.core:space/main",
                Tool = "Place",
                AssetsSearch = "oak",
                Clock = new SavedClock { Day = 3, Hour = 17.5f, Running = true },
                Cameras = new Dictionary<string, SavedPose> { ["forge.core:space/Main"] = new SavedPose { X = 1, Y = 2, Z = -3, RotationW = 1 } },
                Gaps = new Dictionary<string, SavedGap> { ["forge.core:fence/wood"] = new SavedGap { Gap = 0.5f, RandomExtra = 1 } },
            };

            new JsonDocumentFile<WorldViewState>(_path).Save(world);
            var loaded = new JsonDocumentFile<WorldViewState>(_path).Load(out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(loaded.Space, Is.EqualTo("forge.core:space/main"));
            Assert.That(loaded.Tool, Is.EqualTo("Place"));
            Assert.That(loaded.AssetsSearch, Is.EqualTo("oak"));
            Assert.That(loaded.Clock.Day, Is.EqualTo(3));
            Assert.That(loaded.Clock.Hour, Is.EqualTo(17.5f));
            Assert.That(loaded.Clock.Running, Is.True);
            // Keys are data: never camel-cased.
            Assert.That(loaded.Cameras.Keys, Is.EquivalentTo(new[] { "forge.core:space/Main" }));
            Assert.That(loaded.Cameras["forge.core:space/Main"].Z, Is.EqualTo(-3f));
            Assert.That(loaded.Gaps["forge.core:fence/wood"].Gap, Is.EqualTo(0.5f));
        }

        [Test]
        public void Save_WritesCamelCaseAndLeavesUnsetValuesOut()
        {
            new JsonDocumentFile<EditorPrefs>(_path).Save(new EditorPrefs { SideDockOpen = true });

            var text = File.ReadAllText(_path);
            StringAssert.Contains("\"sideDockOpen\": true", text);
            StringAssert.DoesNotContain("flySpeed", text);
        }

        [Test]
        public void Save_LeavesComputedPropertiesOut()
        {
            var world = new WorldViewState { Cameras = new Dictionary<string, SavedPose> { ["core:space/a"] = new SavedPose { X = 1 } } };

            new JsonDocumentFile<WorldViewState>(_path).Save(world);

            var text = File.ReadAllText(_path);
            StringAssert.Contains("\"rotationW\"", text);
            StringAssert.DoesNotContain("\"position\"", text);
            StringAssert.DoesNotContain("\"isValid\"", text);
        }

        [Test]
        public void Load_SkipsFieldsItDoesNotKnow()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{ \"flySpeed\": 3, \"fromANewerBuild\": { \"x\": 1 } }");

            var prefs = new JsonDocumentFile<EditorPrefs>(_path).Load(out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(prefs.FlySpeed, Is.EqualTo(3f));
        }

        [TestCase("{ nope")]
        [TestCase("")]
        [TestCase("{ \"flySpeed\": \"fast\" }")]
        public void Load_UnreadableFile_IsTheDefaultAndSaysWhy(string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, content);

            var prefs = new JsonDocumentFile<EditorPrefs>(_path).Load(out var problem);

            Assert.That(prefs, Is.Not.Null);
            Assert.That(prefs.FlySpeed, Is.Null);
            StringAssert.Contains(_path, problem);
        }

        [Test]
        public void Save_WritesOnlyWhenTheDocumentChanged()
        {
            var file = new JsonDocumentFile<EditorPrefs>(_path);
            var prefs = new EditorPrefs { BrushRadius = 8 };

            Assert.That(file.Save(prefs), Is.True);
            Assert.That(file.Save(prefs), Is.False);
            prefs.BrushRadius = 12;
            Assert.That(file.Save(prefs), Is.True);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_path)), Has.Length.EqualTo(1));
        }

        [Test]
        public void Save_AfterLoadingTheSameDocument_DoesNotWrite()
        {
            new JsonDocumentFile<EditorPrefs>(_path).Save(new EditorPrefs { BrushRadius = 8 });

            var file = new JsonDocumentFile<EditorPrefs>(_path);
            var prefs = file.Load(out _);

            Assert.That(file.Save(prefs), Is.False);
        }

        [Test]
        public void AtomicFile_ReplacesTheOldFileAndLeavesNoTemporaryFile()
        {
            AtomicFile.WriteText(_path, writer => writer.Write("one"));
            AtomicFile.WriteText(_path, writer => writer.Write("two"));

            Assert.That(File.ReadAllText(_path), Is.EqualTo("two"));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_path)), Is.EqualTo(new[] { _path }));
        }
    }
}
