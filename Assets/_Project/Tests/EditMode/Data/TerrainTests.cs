using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class TerrainTests
    {
        [TestCase(17, true)]
        [TestCase(65, true)]
        [TestCase(1025, true)]
        [TestCase(64, false)]
        [TestCase(9, false)]
        [TestCase(2049, false)]
        public void HeightmapResolution_MustBePowerOfTwoPlusOne(int resolution, bool valid)
        {
            Assert.That(WorldGrid.IsValidHeightmapResolution(resolution), Is.EqualTo(valid));
        }

        [Test]
        public void HeightField_IndexesRowMajorByZThenX()
        {
            var field = HeightField.FromFunction((x, z) => x + z * 10f, 17);

            Assert.That(field[3, 0], Is.EqualTo(3f));
            Assert.That(field[0, 1], Is.EqualTo(10f));
            Assert.That(field[16, 1], Is.EqualTo(26f));
        }

        [Test]
        public void HeightField_IsImmutable()
        {
            var source = new float[17 * 17];
            var field = new HeightField(17, source);

            source[0] = 50f;
            field.ToArray()[0] = 60f;

            Assert.That(field[0, 0], Is.EqualTo(0f));
        }

        [Test]
        public void HeightField_RejectsBadInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HeightField.Flat(0f, 64));
            Assert.Throws<ArgumentException>(() => new HeightField(17, new float[10]));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeightField.Flat(-1f, 17));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeightField.Flat(WorldGrid.MaxTerrainHeight + 1f, 17));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeightField.Flat(float.NaN, 17));
        }

        [TestCase(0f, 0f, 0, 0)]
        [TestCase(63.9f, 10f, 0, 0)]
        [TestCase(64f, 0f, 1, 0)]
        [TestCase(-0.01f, 0f, -1, 0)]
        [TestCase(-64f, -64.5f, -1, -2)]
        [TestCase(200f, -130f, 3, -3)]
        public void ChunkOf_FloorsWorldPositionToChunk(float x, float z, int expectedX, int expectedZ)
        {
            Assert.That(WorldGrid.ChunkOf(x, z), Is.EqualTo(new ChunkCoord(expectedX, expectedZ)));
        }

        [TestCase(0, 0, "terrain/core/space/main/c0_0")]
        [TestCase(-1, 2, "terrain/core/space/main/c-1_2")]
        [TestCase(3, -4, "terrain/core/space/main/c3_-4")]
        [TestCase(-5, -6, "terrain/core/space/main/c-5_-6")]
        public void ChunkId_IsDerivedFromSpaceAndCoordinate(int x, int z, string expectedName)
        {
            var id = TerrainChunkRecord.IdFor("forge.core", TestRecords.Space, new ChunkCoord(x, z));

            Assert.That(id.Namespace, Is.EqualTo("forge.core"));
            Assert.That(id.Name, Is.EqualTo(expectedName));
        }

        [Test]
        public void ChunkId_OfAnotherSpace_Differs()
        {
            var coord = new ChunkCoord(0, 0);

            Assert.That(TerrainChunkRecord.IdFor("core", TestRecords.OtherSpace, coord).ToString(), Is.EqualTo("core:terrain/core/space/other/c0_0"));
            Assert.That(TerrainChunkRecord.IdFor("core", TestRecords.OtherSpace, coord), Is.Not.EqualTo(TerrainChunkRecord.IdFor("core", TestRecords.Space, coord)));
        }

        [Test]
        public void ChunkRecord_RequiresMatchingName()
        {
            var coord = new ChunkCoord(1, 1);
            var space = TestRecords.Space;

            Assert.Throws<ArgumentException>(() =>
                new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", space, new ChunkCoord(0, 0)), space, coord, HeightField.Flat(0f, 17)));
            Assert.Throws<ArgumentException>(() =>
                new TerrainChunkRecord(RecordId.Parse("core:terrain/c1_1"), space, coord, HeightField.Flat(0f, 17)), "the old name, without the space");
            Assert.Throws<ArgumentException>(() =>
                new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", TestRecords.OtherSpace, coord), space, coord, HeightField.Flat(0f, 17)),
                "named for another space");
            Assert.DoesNotThrow(() =>
                new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", space, coord), space, coord, HeightField.Flat(0f, 17)));
        }

        [Test]
        public void ChunkRecord_RequiresASpace()
        {
            var coord = new ChunkCoord(0, 0);

            Assert.Throws<ArgumentException>(() =>
                new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", TestRecords.Space, coord), default, coord, HeightField.Flat(0f, 17)));
        }

        [Test]
        public void ChunkRecord_StoresItsSpace_AndWithHeightsKeepsIt()
        {
            var coord = new ChunkCoord(2, -1);
            var chunk = new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", TestRecords.OtherSpace, coord), TestRecords.OtherSpace, coord, HeightField.Flat(0f, 17));

            Assert.That(chunk.Space, Is.EqualTo(TestRecords.OtherSpace));
            Assert.That(chunk.WithHeights(HeightField.Flat(5f, 17)).Space, Is.EqualTo(TestRecords.OtherSpace));
        }
    }
}
