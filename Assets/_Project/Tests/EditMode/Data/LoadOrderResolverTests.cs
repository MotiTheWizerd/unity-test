using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Data
{
    public class LoadOrderResolverTests
    {
        static readonly RecordId Tree = RecordId.Parse("core:tree");

        static ResolvedWorld Resolve(params Plugin[] plugins) => LoadOrderResolver.Resolve(plugins);

        [Test]
        public void SinglePlugin_ExposesItsRecords()
        {
            var world = Resolve(Plugin("core", Base("core:tree"), Placed("core:ref/1", "core:tree")));

            Assert.That(world.Records.Count, Is.EqualTo(2));
            Assert.That(world.Records.Get<BaseObjectRecord>(Tree).DisplayName, Is.EqualTo("Thing"));
            Assert.That(world.Records.OfType<PlacedObjectRecord>().Count(), Is.EqualTo(1));
            Assert.That(world.Records.GetSource(Tree), Is.EqualTo("core"));
            Assert.That(world.LoadOrder, Is.EqualTo(new[] { "core" }));
            Assert.That(world.Conflicts, Is.Empty);
            Assert.That(world.Warnings, Is.Empty);
        }

        [Test]
        public void Override_ByDependentPlugin_WinsWithoutConflict()
        {
            var world = Resolve(
                Plugin("core", Base("core:tree", "Oak")),
                Plugin("mod", new[] { "core" }, Base("core:tree", "Giant Oak")));

            Assert.That(world.Records.Get<BaseObjectRecord>(Tree).DisplayName, Is.EqualTo("Giant Oak"));
            Assert.That(world.Records.GetSource(Tree), Is.EqualTo("mod"));
            Assert.That(world.Conflicts, Is.Empty);
        }

        [Test]
        public void TwoUnrelatedOverrides_LastWinsAndConflictIsReported()
        {
            var world = Resolve(
                Plugin("core", Base("core:tree", "Oak")),
                Plugin("mod_a", new[] { "core" }, Base("core:tree", "Red Oak")),
                Plugin("mod_b", new[] { "core" }, Base("core:tree", "Blue Oak")));

            Assert.That(world.Records.Get<BaseObjectRecord>(Tree).DisplayName, Is.EqualTo("Blue Oak"));
            Assert.That(world.Conflicts, Has.Count.EqualTo(1));
            var conflict = world.Conflicts[0];
            Assert.That(conflict.RecordId, Is.EqualTo(Tree));
            Assert.That(conflict.OverwrittenPluginId, Is.EqualTo("mod_a"));
            Assert.That(conflict.WinningPluginId, Is.EqualTo("mod_b"));
        }

        [Test]
        public void OverrideThatDependsOnPreviousWinner_IsNotAConflict()
        {
            var world = Resolve(
                Plugin("core", Base("core:tree", "Oak")),
                Plugin("mod_a", new[] { "core" }, Base("core:tree", "Red Oak")),
                Plugin("patch", new[] { "core", "mod_a" }, Base("core:tree", "Patched Oak")));

            Assert.That(world.Records.GetSource(Tree), Is.EqualTo("patch"));
            Assert.That(world.Conflicts, Is.Empty);
        }

        [Test]
        public void Deletion_RemovesRecord()
        {
            var world = Resolve(
                Plugin("core", Base("core:tree"), Base("core:rock")),
                Deleting("mod", new[] { "core" }, "core:rock"));

            Assert.That(world.Records.Contains(RecordId.Parse("core:rock")), Is.False);
            Assert.That(world.Records.Contains(Tree), Is.True);
            Assert.That(world.Warnings, Is.Empty);
        }

        [Test]
        public void OverrideOrDeleteOfMissingRecord_ProducesWarnings()
        {
            var world = Resolve(
                Plugin("core", Base("core:tree")),
                Plugin("mod", new[] { "core" }, Base("core:typo")),
                Deleting("mod2", new[] { "core" }, "core:ghost"));

            Assert.That(world.Warnings, Has.Count.EqualTo(2));
        }

        [Test]
        public void MasterLoadedAfterDependent_IsRejected()
        {
            var ex = Assert.Throws<DataValidationException>(() => Resolve(
                Plugin("mod", new[] { "core" }),
                Plugin("core")));

            StringAssert.Contains("must load after", ex.Message);
        }

        [Test]
        public void MissingMasterAndDuplicatePlugin_AreAllReported()
        {
            var ex = Assert.Throws<DataValidationException>(() => Resolve(
                Plugin("core"),
                Plugin("core"),
                Plugin("mod", new[] { "dlc" })));

            Assert.That(ex.Errors.Count, Is.EqualTo(2), ex.Message);
        }

        [Test]
        public void NullLoadOrder_IsRejected()
        {
            Assert.Throws<DataValidationException>(() => LoadOrderResolver.Resolve(null));
            Assert.Throws<DataValidationException>(() => LoadOrderResolver.Resolve(new Plugin[] { null }));
        }

        [Test]
        public void Store_GetWithWrongTypeOrMissingId_Throws()
        {
            var store = Resolve(Plugin("core", Base("core:tree"))).Records;

            Assert.Throws<InvalidCastException>(() => store.Get<PlacedObjectRecord>(Tree));
            Assert.Throws<KeyNotFoundException>(() => store.Get<BaseObjectRecord>(RecordId.Parse("core:none")));
            Assert.Throws<KeyNotFoundException>(() => store.GetSource(RecordId.Parse("core:none")));
            Assert.That(store.TryGet(RecordId.Parse("core:none"), out _), Is.False);
        }
    }
}
