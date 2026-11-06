using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Streaming;
using NUnit.Framework;

namespace Forge.Tests.Streaming
{
    public class GrassScatterTests
    {
        static readonly RecordId Space = RecordId.Parse("core:tamriel");
        static readonly RecordId Grass = GroundLayerRecord.IdFor("core", "grass");
        static readonly RecordId Rock = GroundLayerRecord.IdFor("core", "rock");
        static readonly GrassSettings Meadow = new GrassSettings(2f, 0.5f);

        static GrassSettings GrassOf(RecordId layer) => layer == Grass ? Meadow : null;

        static TerrainChunkRecord Chunk(ChunkPaint paint = null, Func<int, int, float> height = null, int x = 0, int z = 0) =>
            new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(x, z)), Space, new ChunkCoord(x, z),
                HeightField.FromFunction(height ?? ((sx, sz) => 50f), 65), paint);

        static GrassPatch Grow(TerrainChunkRecord chunk, float? sea = null) => GrassScatter.Grow(chunk, GrassOf, Grass, sea);

        [Test]
        public void UnpaintedGround_GrowsTheUnpaintedLayersGrass_AtItsDensity()
        {
            var patch = Grow(Chunk());
            var area = WorldGrid.ChunkSize * WorldGrid.ChunkSize;

            Assert.That(patch.Layers, Is.EqualTo(new[] { Grass }));
            Assert.That(patch.Tufts.Count, Is.EqualTo(Meadow.Density * area));
            Assert.That(patch.Tufts.All(t => t.X >= 0f && t.X < WorldGrid.ChunkSize && t.Z >= 0f && t.Z < WorldGrid.ChunkSize), Is.True);
            Assert.That(patch.Tufts.All(t => Math.Abs(t.Height - Meadow.Height) <= Meadow.Height * GrassScatter.HeightVariation + 1e-5f), Is.True);
            Assert.That(patch.Tufts.All(t => t.Y == 50f), Is.True, "Flat ground: every tuft stands at its height.");
        }

        [Test]
        public void BareLayers_GrowNothing_AndHalfPaintedGrassGrowsAboutHalf()
        {
            Assert.That(Grow(Chunk(GroundPaint.Uniform(Rock))).Tufts, Is.Empty);
            Assert.That(GrassScatter.Grow(Chunk(), GrassOf, null, null).Tufts, Is.Empty, "No unpainted layer: unpainted ground grows nothing.");

            var weights = new float[2 * GroundPaint.TexelCount];
            for (var t = 0; t < GroundPaint.TexelCount; t++)
                weights[t] = weights[GroundPaint.TexelCount + t] = 1f;
            var half = Grow(Chunk(GroundPaint.FromWeights(new[] { Grass, Rock }, weights)));

            var full = Meadow.Density * WorldGrid.ChunkSize * WorldGrid.ChunkSize;
            Assert.That(half.Tufts.Count, Is.EqualTo(full / 2f).Within(full * 0.03f));
            Assert.That(half.Tufts.All(t => half.Layers[t.Layer] == Grass), Is.True);
        }

        [Test]
        public void Tufts_StandOnSlopes_AndNoneUnderTheSea()
        {
            // Height rises 0.5 m per meter east: 30 m on the west edge to 62 m on the east.
            var slope = Chunk(height: (x, z) => 30f + x * 0.5f);
            var patch = Grow(slope);
            Assert.That(patch.Tufts.All(t => Math.Abs(t.Y - (30f + t.X * 0.5f)) < 1e-3f), Is.True);

            var wet = Grow(slope, sea: 46f);
            Assert.That(wet.Tufts, Is.Not.Empty);
            Assert.That(wet.Tufts.All(t => t.Y >= 46f), Is.True);
            Assert.That(wet.Tufts.Count, Is.LessThan(patch.Tufts.Count));
        }

        [Test]
        public void TheSameChunk_AlwaysGrowsTheSameGrass_AndItsNeighbourOther()
        {
            var a = Grow(Chunk()).Tufts;
            var b = Grow(Chunk()).Tufts;
            var neighbour = Grow(Chunk(x: 1)).Tufts;

            Assert.That(a.Select(t => (t.X, t.Z, t.Height, t.Yaw)), Is.EqualTo(b.Select(t => (t.X, t.Z, t.Height, t.Yaw))));
            Assert.That(a.Select(t => (t.X, t.Z)).Take(50), Is.Not.EqualTo(neighbour.Select(t => (t.X, t.Z)).Take(50)));
        }
    }
}
