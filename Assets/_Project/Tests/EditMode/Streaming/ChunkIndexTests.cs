using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Streaming;
using Forge.Tests.Data;
using NUnit.Framework;

namespace Forge.Tests.Streaming
{
    public class ChunkIndexTests
    {
        [Test]
        public void GroupsObjectsByTheChunkUnderTheirPosition()
        {
            var index = ChunkIndex.Build(Store(TestRecords.Plugin("core",
                TestRecords.Base("core:rock"),
                PlacedAt("core:ref/a", 10f, 10f),
                PlacedAt("core:ref/b", 70f, 10f),
                PlacedAt("core:ref/c", -5f, 130f),
                PlacedAt("core:ref/d", 63f, 1f))));

            Assert.That(Names(index.ObjectsIn(new ChunkCoord(0, 0))), Is.EqualTo(new[] { "ref/a", "ref/d" }));
            Assert.That(Names(index.ObjectsIn(new ChunkCoord(1, 0))), Is.EqualTo(new[] { "ref/b" }));
            Assert.That(Names(index.ObjectsIn(new ChunkCoord(-1, 2))), Is.EqualTo(new[] { "ref/c" }));
            Assert.That(index.ObjectsIn(new ChunkCoord(5, 5)), Is.Empty);
            Assert.That(index.ObjectCount, Is.EqualTo(4));
        }

        [Test]
        public void IndexesTerrainByCoordinate()
        {
            var index = ChunkIndex.Build(Store(TestRecords.Plugin("core", Chunk("core", 0, 0), Chunk("core", -1, 3))));

            Assert.That(index.TerrainCount, Is.EqualTo(2));
            Assert.That(index.TryGetTerrain(new ChunkCoord(-1, 3), out var chunk), Is.True);
            Assert.That(chunk.Id.ToString(), Is.EqualTo("core:terrain/c-1_3"));
            Assert.That(index.TryGetTerrain(new ChunkCoord(1, 1), out _), Is.False);
        }

        [Test]
        public void DuplicateCoordinate_LowestIdWins_OthersAreShadowed()
        {
            var core = TestRecords.Plugin("core", Chunk("core", 0, 0));
            var mod = TestRecords.Plugin("amod", new[] { "core" }, Chunk("amod", 0, 0));
            var world = LoadOrderResolver.Resolve(new[] { core, mod });

            var index = ChunkIndex.Build(world.Records);

            Assert.That(index.TryGetTerrain(new ChunkCoord(0, 0), out var winner), Is.True);
            Assert.That(winner.Id.Namespace, Is.EqualTo("amod"));
            Assert.That(index.ShadowedTerrain.Select(c => c.Id.ToString()), Is.EqualTo(new[] { "core:terrain/c0_0" }));
        }

        [Test]
        public void EmptyIndex_HasNothing()
        {
            Assert.That(ChunkIndex.Empty.TerrainCount, Is.Zero);
            Assert.That(ChunkIndex.Empty.ObjectsIn(new ChunkCoord(0, 0)), Is.Empty);
        }

        static IRecordStore Store(Plugin plugin) => LoadOrderResolver.Resolve(new[] { plugin }).Records;

        static PlacedObjectRecord PlacedAt(string id, float x, float z) =>
            new PlacedObjectRecord(RecordId.Parse(id), RecordId.Parse("core:rock"), new Vector3(x, 0f, z), Quaternion.Identity, 1f);

        static TerrainChunkRecord Chunk(string ns, int x, int z)
        {
            var coord = new ChunkCoord(x, z);
            return new TerrainChunkRecord(TerrainChunkRecord.IdFor(ns, coord), coord, HeightField.Flat(0f, 17));
        }

        static string[] Names(System.Collections.Generic.IEnumerable<PlacedObjectRecord> records) =>
            records.Select(r => r.Id.Name).ToArray();
    }
}
