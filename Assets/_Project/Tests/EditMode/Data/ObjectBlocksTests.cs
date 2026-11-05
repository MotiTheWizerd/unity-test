using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Data
{
    /// <summary>Placed objects kept and resolved a chunk at a time: blocks, the fold across plugins, and the layered view.</summary>
    public class ObjectBlocksTests
    {
        static readonly RecordId Rock = RecordId.Parse("core:rock");

        static PlacedObjectRecord At(string id, float x, float z, RecordId space = default) =>
            new PlacedObjectRecord(RecordId.Parse(id), Rock, space.IsDefault ? Space : space, new Vector3(x, 0f, z), Quaternion.Identity, 1f);

        static ObjectBlockKey Key(int x, int z, RecordId space = default) => new ObjectBlockKey(space.IsDefault ? Space : space, new ChunkCoord(x, z));

        [Test]
        public void InMemoryBlocks_GroupByChunk_SortById_AndKnowTheirOverrides()
        {
            var blocks = new InMemoryObjectBlocks("mod", new[] { At("mod:ref/b", 70f, 1f), At("mod:ref/a", 1f, 1f), At("core:ref/x", 2f, 2f), At("mod:ref/far", 5f, 5f, OtherSpace) });

            Assert.That(blocks.Keys, Is.EqualTo(new[] { Key(0, 0), Key(1, 0), Key(0, 0, OtherSpace) }));
            Assert.That((blocks.TotalCount, blocks.CountOf(Key(0, 0)), blocks.CountOf(Key(9, 9))), Is.EqualTo((4, 2, 0)));
            Assert.That(blocks.Read(Key(0, 0)).Select(p => p.Id.ToString()), Is.EqualTo(new[] { "core:ref/x", "mod:ref/a" }));
            Assert.That(blocks.Overrides.Select(id => id.ToString()), Is.EqualTo(new[] { "core:ref/x" }));
            Assert.That(blocks.TryLocate(RecordId.Parse("mod:ref/b"), out var key) && key == Key(1, 0), Is.True);
            Assert.That(blocks.TryLocate(RecordId.Parse("mod:ref/none"), out _), Is.False);
        }

        [Test]
        public void Plugin_KeepsItsObjectsApartFromItsRecords()
        {
            var plugin = Plugin("core", Base("core:rock"), At("core:ref/a", 1f, 1f));

            Assert.That(plugin.Records.Select(r => r.Id.ToString()), Is.EqualTo(new[] { "core:rock" }));
            Assert.That(plugin.Objects.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void Fold_AnObjectMovedToAnotherChunkByALaterPlugin_ShowsOnlyThere()
        {
            var core = Plugin("core", Base("core:rock"), At("core:ref/a", 1f, 1f), At("core:ref/b", 2f, 2f));
            var mod = Plugin("mod", new[] { "core" }, At("core:ref/a", 100f, 1f));
            var records = LoadOrderResolver.Resolve(new[] { core, mod }).Records;

            Assert.That(records.ObjectsIn(Space, new ChunkCoord(0, 0)).Select(p => p.Id.Name), Is.EqualTo(new[] { "ref/b" }));
            Assert.That(records.ObjectsIn(Space, new ChunkCoord(1, 0)).Select(p => p.Id.Name), Is.EqualTo(new[] { "ref/a" }));
            Assert.That(records.ObjectChunks(Space), Is.EquivalentTo(new[] { new ChunkCoord(0, 0), new ChunkCoord(1, 0) }));
            Assert.That(records.Get<PlacedObjectRecord>(RecordId.Parse("core:ref/a")).Position.X, Is.EqualTo(100f));
            Assert.That(records.GetSource(RecordId.Parse("core:ref/a")), Is.EqualTo("mod"));
            Assert.That(records.Count, Is.EqualTo(3), "The base object and two placed objects.");
        }

        [Test]
        public void Fold_ADeletedObject_IsGone_AndOneWrittenAgainAfterwards_IsBack()
        {
            var core = Plugin("core", Base("core:rock"), At("core:ref/a", 1f, 1f));
            var eraser = Deleting("eraser", new[] { "core" }, "core:ref/a");
            var restorer = Plugin("restorer", new[] { "core", "eraser" }, At("core:ref/a", 3f, 3f));

            var erased = LoadOrderResolver.Resolve(new[] { core, eraser }).Records;
            Assert.That(erased.ObjectsIn(Space, new ChunkCoord(0, 0)), Is.Empty);
            Assert.That(erased.Contains(RecordId.Parse("core:ref/a")), Is.False);
            Assert.That(erased.Count, Is.EqualTo(1));

            var restored = LoadOrderResolver.Resolve(new[] { core, eraser, restorer }).Records;
            Assert.That(restored.ObjectsIn(Space, new ChunkCoord(0, 0)).Single().Position.X, Is.EqualTo(3f));
            Assert.That(restored.GetSource(RecordId.Parse("core:ref/a")), Is.EqualTo("restorer"));
            Assert.That(restored.Count, Is.EqualTo(2));
        }

        [Test]
        public void Fold_WarnsAndConflictsLikeTheOtherRecords()
        {
            var core = Plugin("core", Base("core:rock"), At("core:ref/a", 1f, 1f));
            var mod = Plugin("mod", new[] { "core" }, At("core:ref/a", 2f, 2f), At("core:ref/ghost", 2f, 2f));
            var patch = Plugin("patch", new[] { "core" }, At("core:ref/a", 4f, 4f));

            var world = LoadOrderResolver.Resolve(new[] { core, mod, patch });

            Assert.That(world.Warnings, Is.EqualTo(new[] { "'mod' overrides 'core:ref/ghost', which no earlier plugin defines." }));
            Assert.That(world.Conflicts.Select(c => c.ToString()).ToArray(), Has.Length.EqualTo(1).And.All.Contains("core:ref/a"));
            Assert.That(world.Records.Count, Is.EqualTo(3), "rock, a (patch's), ghost");
        }

        [Test]
        public void Layered_SeesTheTopPluginsObjects_AndHidesWhatItOverridesOrDeletes()
        {
            var core = Plugin("core", Base("core:rock"), At("core:ref/a", 1f, 1f), At("core:ref/b", 2f, 2f), At("core:ref/c", 70f, 1f));
            var layer = MasterLayer.Build(new[] { core });
            var top = new Plugin(new PluginManifest("top", "1.0.0", new[] { "core" }),
                new Record[] { At("core:ref/a", 80f, 1f), At("top:ref/new", 3f, 3f) },
                new[] { RecordId.Parse("core:ref/b") });

            var records = layer.Overlay(top).Records;

            Assert.That(records.ObjectsIn(Space, new ChunkCoord(0, 0)).Select(p => p.Id.ToString()), Is.EqualTo(new[] { "top:ref/new" }));
            Assert.That(records.ObjectsIn(Space, new ChunkCoord(1, 0)).Select(p => p.Id.ToString()), Is.EqualTo(new[] { "core:ref/a", "core:ref/c" }));
            Assert.That(records.ObjectChunks(Space), Is.EquivalentTo(new[] { new ChunkCoord(0, 0), new ChunkCoord(1, 0) }));
            Assert.That(records.Count, Is.EqualTo(4), "rock, a, c, new");
            Assert.That(records.ObjectsIn(Space, new ChunkCoord(5, 5)), Is.Empty);
        }

        [Test]
        public void Fold_ReadsABlockOnce_AndOnlyWhenAsked()
        {
            var counting = new CountingBlocks("core", new[] { At("core:ref/a", 1f, 1f), At("core:ref/b", 70f, 1f) });
            var core = new Plugin(new PluginManifest("core", "1.0.0"), new[] { Base("core:rock") }, null, counting);
            var records = LoadOrderResolver.Resolve(new[] { core }).Records;

            Assert.That(counting.Reads, Is.EqualTo(0), "Resolving reads nothing.");
            Assert.That(records.Count, Is.EqualTo(3), "Counting reads nothing either.");
            records.ObjectsIn(Space, new ChunkCoord(0, 0));
            records.ObjectsIn(Space, new ChunkCoord(0, 0));
            Assert.That(counting.Reads, Is.EqualTo(1));
            Assert.That(records.TryGet(RecordId.Parse("core:ref/b"), out _), Is.True);
            Assert.That(counting.Reads, Is.EqualTo(2), "Finding an object reads its block.");
        }

        sealed class CountingBlocks : IObjectBlocks
        {
            readonly InMemoryObjectBlocks _inner;

            public CountingBlocks(string pluginId, PlacedObjectRecord[] objects) => _inner = new InMemoryObjectBlocks(pluginId, objects);

            public int Reads { get; private set; }

            public System.Collections.Generic.IReadOnlyList<ObjectBlockKey> Keys => _inner.Keys;
            public int TotalCount => _inner.TotalCount;
            public System.Collections.Generic.IReadOnlyCollection<RecordId> Overrides => _inner.Overrides;
            public int CountOf(ObjectBlockKey key) => _inner.CountOf(key);
            public bool TryLocate(RecordId id, out ObjectBlockKey key) => _inner.TryLocate(id, out key);

            public System.Collections.Generic.IReadOnlyList<PlacedObjectRecord> Read(ObjectBlockKey key)
            {
                if (_inner.CountOf(key) > 0)
                    Reads++;
                return _inner.Read(key);
            }
        }
    }
}
