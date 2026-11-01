using System;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Data
{
    public class PluginFileStoreTests
    {
        string _directory;
        PluginFileStore _store;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "forge-tests-" + Guid.NewGuid().ToString("N"));
            _store = new PluginFileStore(_directory, new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            _store.Save(Plugin("core", Base("core:tree", "Oak")));

            var loaded = _store.Load("core");

            Assert.That(_store.Exists("core"), Is.True);
            Assert.That(((BaseObjectRecord)loaded.Records.Single()).DisplayName, Is.EqualTo("Oak"));
        }

        [Test]
        public void Save_OverwritesExistingFileWithoutLeftovers()
        {
            _store.Save(Plugin("core", Base("core:tree", "Oak")));
            _store.Save(Plugin("core", Base("core:tree", "Pine")));

            Assert.That(((BaseObjectRecord)_store.Load("core").Records.Single()).DisplayName, Is.EqualTo("Pine"));
            Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        }

        [Test]
        public void LoadAll_ReturnsEveryPluginAndToleratesMissingDirectory()
        {
            Assert.That(_store.LoadAll(), Is.Empty);

            _store.Save(Plugin("core"));
            _store.Save(Plugin("mod", new[] { "core" }));

            Assert.That(_store.LoadAll().Select(p => p.Id), Is.EquivalentTo(new[] { "core", "mod" }));
        }

        [Test]
        public void Load_FileNameMismatch_IsRejected()
        {
            _store.Save(Plugin("core"));
            File.Move(_store.PathFor("core"), _store.PathFor("renamed"));

            var ex = Assert.Throws<DataValidationException>(() => _store.Load("renamed"));
            StringAssert.Contains("file name must match", ex.Message);
        }

        [Test]
        public void Load_CorruptFile_ReportsFileName()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_store.PathFor("broken"), "{ nope");

            var ex = Assert.Throws<DataValidationException>(() => _store.Load("broken"));
            StringAssert.Contains("broken.json", ex.Message);
        }
    }
}
