using System;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
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

        [TestCase(0, 0, "terrain/c0_0")]
        [TestCase(-1, 2, "terrain/c-1_2")]
        [TestCase(3, -4, "terrain/c3_-4")]
        [TestCase(-5, -6, "terrain/c-5_-6")]
        public void ChunkId_IsDerivedFromCoordinate(int x, int z, string expectedName)
        {
            var id = TerrainChunkRecord.IdFor("forge.core", new ChunkCoord(x, z));

            Assert.That(id.Name, Is.EqualTo(expectedName));
        }

        [Test]
        public void ChunkRecord_RequiresMatchingName()
        {
            var coord = new ChunkCoord(1, 1);

            Assert.Throws<ArgumentException>(() =>
                new TerrainChunkRecord(RecordId.Parse("core:terrain/c0_0"), coord, HeightField.Flat(0f, 17)));
            Assert.DoesNotThrow(() =>
                new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", coord), coord, HeightField.Flat(0f, 17)));
        }

        [Test]
        public void ChunkRecord_RoundTripsThroughJson()
        {
            var coord = new ChunkCoord(-2, 3);
            var heights = HeightField.FromFunction((x, z) => (x * 7 + z * 13) % 150 + 0.25f, 33);
            var plugin = new Plugin(
                new PluginManifest("core", "1.0.0"),
                new[] { new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", coord), coord, heights) });
            var serializer = new JsonPluginSerializer(RecordTypeRegistry.CreateDefault());

            string json;
            using (var writer = new StringWriter())
            {
                serializer.Write(plugin, writer);
                json = writer.ToString();
            }

            Plugin copy;
            using (var reader = new StringReader(json))
                copy = serializer.Read(reader);

            var chunk = (TerrainChunkRecord)copy.Records.Single();
            Assert.That(chunk.Coord, Is.EqualTo(coord));
            Assert.That(chunk.Heights.Resolution, Is.EqualTo(33));
            Assert.That(chunk.Heights.ToArray(), Is.EqualTo(heights.ToArray()));
            StringAssert.Contains("\"type\": \"terrain_chunk\"", json);
        }
    }
}
