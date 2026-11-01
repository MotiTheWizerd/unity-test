using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    /// <summary>
    /// The editor resolves masters once and layers the active plugin over them.
    /// These tests pin that it gives exactly what a full resolution would.
    /// </summary>
    public class MasterLayerTests
    {
        static readonly string[] CoreIds = Enumerable.Range(0, 12).Select(i => $"core:ref/o{i:D2}").ToArray();

        // core defines objects; amod (master: core) overrides some and deletes one,
        // so a top plugin that only lists core as master runs into conflicts.
        static IReadOnlyList<Plugin> Masters()
        {
            var core = TestRecords.Plugin("core",
                CoreIds.Select(id => (Record)TestRecords.Placed(id, "core:rock")).Prepend(TestRecords.Base("core:rock")).ToArray());
            var mod = new Plugin(
                new PluginManifest("amod", "1.0.0", new[] { "core" }),
                new Record[] { TestRecords.Placed(CoreIds[0], "core:rock", 5f), TestRecords.Placed(CoreIds[1], "core:rock", 6f), TestRecords.Placed("amod:ref/own", "core:rock") },
                new[] { RecordId.Parse(CoreIds[2]) });
            return new[] { core, mod };
        }

        [Test]
        public void RandomTopPlugins_ResolveExactlyLikeTheFullLoadOrder()
        {
            var masters = Masters();
            var layer = MasterLayer.Build(masters);
            var random = new System.Random(11);
            // Includes ids no master defines ("core:ref/ghost..."), to exercise the warnings.
            var candidates = CoreIds.Concat(new[] { "core:ref/ghost1", "core:ref/ghost2" }).ToArray();

            for (var round = 0; round < 200; round++)
            {
                var records = new List<Record>();
                var deletions = new List<RecordId>();
                foreach (var id in candidates)
                {
                    switch (random.Next(4))
                    {
                        case 0: records.Add(TestRecords.Placed(id, "core:rock", random.Next(100))); break;
                        case 1: deletions.Add(RecordId.Parse(id)); break;
                    }
                }

                for (var i = random.Next(3); i > 0; i--)
                    records.Add(TestRecords.Placed($"top:ref/new{i}", "core:rock"));

                var top = new Plugin(new PluginManifest("top", "1.0.0", new[] { "core" }), records, deletions);

                AssertSameWorld(layer.Overlay(top), LoadOrderResolver.Resolve(masters.Append(top).ToArray()), $"round {round}");
            }
        }

        [Test]
        public void Overlay_ValidatesTheTopPluginsMasters()
        {
            var layer = MasterLayer.Build(Masters());
            var top = new Plugin(new PluginManifest("top", "1.0.0", new[] { "missing" }), null);

            Assert.Throws<DataValidationException>(() => layer.Overlay(top));
        }

        [Test]
        public void ChangesSince_ListsExactlyTheIdsThatDiffer()
        {
            var layer = MasterLayer.Build(Masters());
            var moved = TestRecords.Placed(CoreIds[3], "core:rock", 9f);
            var kept = TestRecords.Placed(CoreIds[4], "core:rock", 9f);
            var first = layer.Overlay(Top(new Record[] { kept }, new[] { CoreIds[5] }));
            var second = layer.Overlay(Top(new Record[] { kept, moved }, new[] { CoreIds[6] }));

            Assert.That(second.TryGetChangesSince(first, out var changed), Is.True);
            Assert.That(changed.Select(id => id.ToString()), Is.EquivalentTo(new[] { CoreIds[3], CoreIds[5], CoreIds[6] }));
        }

        [Test]
        public void ChangesSince_IsUnknown_ForWorldsNotLayeredOnTheSameMasters()
        {
            var masters = Masters();
            var layered = MasterLayer.Build(masters).Overlay(Top(new Record[0], new string[0]));
            var otherLayer = MasterLayer.Build(masters).Overlay(Top(new Record[0], new string[0]));
            var full = LoadOrderResolver.Resolve(masters);

            Assert.That(layered.TryGetChangesSince(full, out _), Is.False);
            Assert.That(layered.TryGetChangesSince(null, out _), Is.False);
            Assert.That(layered.TryGetChangesSince(otherLayer, out _), Is.False);
        }

        static Plugin Top(Record[] records, string[] deletions) =>
            new Plugin(new PluginManifest("top", "1.0.0", new[] { "core" }), records, deletions.Select(RecordId.Parse));

        static void AssertSameWorld(ResolvedWorld actual, ResolvedWorld expected, string context)
        {
            Assert.That(actual.LoadOrder, Is.EqualTo(expected.LoadOrder), context);
            Assert.That(actual.Records.Count, Is.EqualTo(expected.Records.Count), context);
            Assert.That(actual.Records.All.Select(r => r.Id), Is.EquivalentTo(expected.Records.All.Select(r => r.Id)), context);
            Assert.That(actual.Records.OfType<PlacedObjectRecord>().Count(), Is.EqualTo(expected.Records.OfType<PlacedObjectRecord>().Count()), context);
            foreach (var record in expected.Records.All)
            {
                Assert.That(actual.Records.TryGet(record.Id, out var found) && ReferenceEquals(found, record), Is.True, $"{context}: {record.Id}");
                Assert.That(actual.Records.GetSource(record.Id), Is.EqualTo(expected.Records.GetSource(record.Id)), $"{context}: source of {record.Id}");
            }

            foreach (var id in CoreIds.Select(RecordId.Parse))
                Assert.That(actual.Records.Contains(id), Is.EqualTo(expected.Records.Contains(id)), $"{context}: contains {id}");

            Assert.That(actual.Conflicts.Select(c => c.ToString()), Is.EqualTo(expected.Conflicts.Select(c => c.ToString())), context);
            Assert.That(actual.Warnings, Is.EqualTo(expected.Warnings), context);
        }
    }
}
