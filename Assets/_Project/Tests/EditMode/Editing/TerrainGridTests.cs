using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class TerrainGridTests
    {
        const int Resolution = 17; // 4 m between samples

        static TerrainChunkRecord Chunk(int x, int z, Func<int, int, float> heightAt, int resolution = Resolution, RecordId space = default)
        {
            if (space.IsDefault)
                space = Space;
            return new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", space, new ChunkCoord(x, z)), space, new ChunkCoord(x, z),
                HeightField.FromFunction(heightAt, resolution));
        }

        static TerrainGrid Grid(params TerrainChunkRecord[] chunks) => Grid(Space, chunks);

        static TerrainGrid Grid(RecordId space, params TerrainChunkRecord[] chunks) =>
            new TerrainGrid(new EditSession(new[] { Plugin("core", chunks) }, Plugin("user", new[] { "core" })).World.Records, space);

        [Test]
        public void HeightAt_IsBilinearBetweenSamples()
        {
            var grid = Grid(Chunk(0, 0, (x, z) => x + 10f * z));

            Assert.That(grid.Spacing, Is.EqualTo(4f));
            Assert.That(grid.HeightAt(new Vector2(6f, 0f)), Is.EqualTo(1.5f).Within(1e-5f), "Halfway between samples 1 and 2.");
            Assert.That(grid.HeightAt(new Vector2(4f, 2f)), Is.EqualTo(1f + 5f).Within(1e-5f), "Halfway between rows 0 and 1.");
        }

        [Test]
        public void ASampleOnAChunkEdge_IsReadFromTheChunkThatStartsThere()
        {
            var grid = Grid(Chunk(0, 0, (x, z) => 10f), Chunk(1, 0, (x, z) => 20f));

            Assert.That(grid.Locate(16, 3), Is.EqualTo((new ChunkCoord(1, 0), 0, 3)));
            Assert.That(grid.SampleHeight(16, 3), Is.EqualTo(20f));
            Assert.That(grid.SampleHeight(15, 3), Is.EqualTo(10f));
        }

        [Test]
        public void OutsideTheTerrain_ThereIsNoHeight()
        {
            var grid = Grid(Chunk(0, 0, (x, z) => 5f));

            Assert.That(grid.SampleHeight(-1, 0), Is.Null);
            Assert.That(grid.HeightAt(new Vector2(-2f, 8f)), Is.Null);
            Assert.That(grid.HeightAt(new Vector2(8f, 8f)), Is.EqualTo(5f));
        }

        [Test]
        public void MixedResolutions_AreRejected()
        {
            Assert.Throws<InvalidOperationException>(() => Grid(Chunk(0, 0, (x, z) => 1f), Chunk(1, 0, (x, z) => 1f, 33)));
        }

        [Test]
        public void OnlyTheGridsOwnSpace_IsRead()
        {
            var chunks = new[]
            {
                Chunk(0, 0, (x, z) => 5f),
                Chunk(0, 0, (x, z) => 50f, 33, OtherSpace),
                Chunk(1, 0, (x, z) => 50f, 33, OtherSpace),
            };

            var grid = Grid(Space, chunks);
            var other = Grid(OtherSpace, chunks);

            Assert.That(grid.Space, Is.EqualTo(Space));
            Assert.That(grid.HeightAt(new Vector2(8f, 8f)), Is.EqualTo(5f), "A chunk at the same coordinate in another space is ignored.");
            Assert.That(grid.Contains(new ChunkCoord(1, 0)), Is.False);
            Assert.That(grid.Resolution, Is.EqualTo(Resolution), "Other spaces' resolutions don't clash with this one's.");
            Assert.That(other.HeightAt(new Vector2(8f, 8f)), Is.EqualTo(50f));
            Assert.That(other.Contains(new ChunkCoord(1, 0)), Is.True);
        }
    }
}
