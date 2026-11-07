using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class LandHeightsTests
    {
        static TerrainChunkRecord Chunk(int x, HeightField heights) =>
            new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(x, 0)), Space, new ChunkCoord(x, 0), heights);

        [Test]
        public void HighestIn_IsTheTopOfTheHighestChunk()
        {
            var store = new EditSession(new[] { Plugin("core", Chunk(0, HeightField.Flat(50f)), Chunk(1, HeightField.FromFunction((x, z) => 50f + x, 65))) },
                Plugin("user", new[] { "core" })).World.Records;

            Assert.That(LandHeights.HighestIn(store, Space), Is.EqualTo(114f));
        }

        [Test]
        public void HighestIn_ASpaceWithoutLand_IsNull()
        {
            var store = new EditSession(new[] { Plugin("core", Chunk(0, HeightField.Flat(50f))) }, Plugin("user", new[] { "core" })).World.Records;

            Assert.That(LandHeights.HighestIn(store, new RecordId("core", "space/elsewhere")), Is.Null);
        }

        [Test]
        public void HighestIn_FollowsSculpting()
        {
            var session = new EditSession(new[] { Plugin("core", Chunk(0, HeightField.Flat(50f))) }, Plugin("user", new[] { "core" }));
            Assert.That(LandHeights.HighestIn(session.World.Records, Space), Is.EqualTo(50f));

            session.Put(Chunk(0, HeightField.Flat(80f)));

            Assert.That(LandHeights.HighestIn(session.World.Records, Space), Is.EqualTo(80f));
        }
    }
}
