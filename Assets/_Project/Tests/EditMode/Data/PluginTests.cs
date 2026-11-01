using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Data
{
    public class PluginTests
    {
        [Test]
        public void Manifest_RejectsInvalidIdsAndSelfReference()
        {
            var ex = Assert.Throws<DataValidationException>(() =>
                new PluginManifest("my.mod", "1.0.0", new[] { "my.mod", "Bad Id", "core", "core" }));

            Assert.That(ex.Errors.Count, Is.EqualTo(3), ex.Message);
            Assert.Throws<DataValidationException>(() => new PluginManifest("Bad", "1.0.0"));
            Assert.Throws<DataValidationException>(() => new PluginManifest("ok", " "));
        }

        [Test]
        public void Plugin_SortsRecordsAndDeletionsById()
        {
            var plugin = Plugin("core", Base("core:b"), Base("core:a"));

            Assert.That(plugin.Records.Select(r => r.Id.ToString()), Is.EqualTo(new[] { "core:a", "core:b" }));
        }

        [Test]
        public void Plugin_MayWriteOwnAndMasterNamespacesOnly()
        {
            Assert.DoesNotThrow(() => Plugin("mod", new[] { "core" }, Base("mod:new"), Base("core:tree")));

            var ex = Assert.Throws<DataValidationException>(() => Plugin("mod", Base("core:tree")));
            StringAssert.Contains("core:tree", ex.Message);
        }

        [Test]
        public void Plugin_MayDeleteOnlyMasterRecords()
        {
            Assert.DoesNotThrow(() => Deleting("mod", new[] { "core" }, "core:tree"));
            Assert.Throws<DataValidationException>(() => Deleting("mod", null, "mod:own"));
        }

        [Test]
        public void Plugin_ReportsAllProblemsAtOnce()
        {
            var ex = Assert.Throws<DataValidationException>(() =>
                new Plugin(
                    new PluginManifest("mod", "1.0.0", new[] { "core" }),
                    new[] { Base("mod:a"), Base("mod:a"), Base("core:x"), Base("other:y") },
                    new[] { RecordId.Parse("core:x") }));

            // duplicate mod:a, foreign namespace other:y, both writes and deletes core:x
            Assert.That(ex.Errors.Count, Is.EqualTo(3), ex.Message);
        }

        [Test]
        public void Plugin_RejectsNullRecord()
        {
            Assert.Throws<DataValidationException>(() => Plugin("core", Base("core:a"), null));
        }

        [Test]
        public void DataValidationException_ListsEveryError()
        {
            var ex = new DataValidationException(new[] { "first", "second" });

            Assert.That(ex.Errors, Is.EqualTo(new[] { "first", "second" }));
            StringAssert.Contains("first", ex.Message);
            StringAssert.Contains("second", ex.Message);
        }

        [Test]
        public void Plugin_RequiresManifest()
        {
            Assert.Throws<ArgumentNullException>(() => new Plugin(null, null));
        }
    }
}
