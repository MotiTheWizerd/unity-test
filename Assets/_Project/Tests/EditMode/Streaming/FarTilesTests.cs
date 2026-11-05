using System;
using System.Collections.Generic;
using Forge.Data.Terrain;
using Forge.Streaming;
using NUnit.Framework;

namespace Forge.Tests.Streaming
{
    public class FarTilesTests
    {
        const int ChunksPerTile = 4;
        const float Step = 8f;

        [Test]
        public void TileOf_FloorsNegativeCoordinates()
        {
            Assert.That(FarTiles.TileOf(new ChunkCoord(0, 0), ChunksPerTile), Is.EqualTo(new ChunkCoord(0, 0)));
            Assert.That(FarTiles.TileOf(new ChunkCoord(3, 4), ChunksPerTile), Is.EqualTo(new ChunkCoord(0, 1)));
            Assert.That(FarTiles.TileOf(new ChunkCoord(-1, -4), ChunksPerTile), Is.EqualTo(new ChunkCoord(-1, -1)));
            Assert.That(FarTiles.TileOf(new ChunkCoord(-5, 7), ChunksPerTile), Is.EqualTo(new ChunkCoord(-2, 1)));
        }

        [Test]
        public void Sample_TakesEveryStrideThSampleOfEachChunk()
        {
            // Height = world x + 2 · world z (in meters), so every sample can be checked against its position.
            var tile = FarTiles.Sample(new ChunkCoord(1, -1), ChunksPerTile, Step, coord => Plane(coord, 65));

            Assert.That(tile.Resolution, Is.EqualTo(33));
            Assert.That(tile.Cells, Is.EqualTo(32));
            Assert.That(tile.Size, Is.EqualTo(256f));
            Assert.That(tile.IsEmpty, Is.False);
            for (var z = 0; z < tile.Resolution; z++)
            for (var x = 0; x < tile.Resolution; x++)
            {
                var worldX = 1 * 256f + x * Step;
                var worldZ = -1 * 256f + z * Step;
                Assert.That(tile[x, z], Is.EqualTo(Expected(worldX, worldZ)).Within(1e-3f), $"sample ({x}, {z})");
            }
        }

        [Test]
        public void Sample_HandlesEachChunksOwnSpacing()
        {
            // 2 m chunks (33 samples): the stride is 4 instead of 8; the far heights are the same positions.
            var tile = FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, Step, coord => Plane(coord, 33));

            Assert.That(tile[0, 0], Is.EqualTo(Expected(0f, 0f)).Within(1e-3f));
            Assert.That(tile[32, 32], Is.EqualTo(Expected(256f, 256f)).Within(1e-3f));
            Assert.That(tile[9, 20], Is.EqualTo(Expected(72f, 160f)).Within(1e-3f));
        }

        [Test]
        public void Sample_MissingChunks_LeaveHoles_AndKeepTheirNeighboursEdges()
        {
            // Only chunk (1, 1) of the tile exists.
            var tile = FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, Step,
                coord => coord == new ChunkCoord(1, 1) ? Plane(coord, 65) : null);

            Assert.That(tile.HasChunk(1, 1), Is.True);
            Assert.That(tile.HasChunk(0, 0), Is.False);
            Assert.That(tile.HasChunk(2, 1), Is.False);
            Assert.That(tile.IsEmpty, Is.False);
            // Cells of the one chunk: samples 8..16 on both axes, cells 8..15.
            Assert.That(tile.CellHasLand(8, 8), Is.True);
            Assert.That(tile.CellHasLand(15, 15), Is.True);
            Assert.That(tile.CellHasLand(7, 8), Is.False);
            Assert.That(tile.CellHasLand(16, 8), Is.False);
            // Its boundary samples, shared with the missing neighbours, carry its heights.
            Assert.That(tile[8, 8], Is.EqualTo(Expected(64f, 64f)).Within(1e-3f));
            Assert.That(tile[16, 16], Is.EqualTo(Expected(128f, 128f)).Within(1e-3f));
            Assert.That(tile[16, 12], Is.EqualTo(Expected(128f, 96f)).Within(1e-3f));
            // Samples of missing chunks are 0.
            Assert.That(tile[0, 0], Is.EqualTo(0f));
            Assert.That(tile[30, 30], Is.EqualTo(0f));
        }

        [Test]
        public void Sample_EastAndNorthNeighbours_FillTheTilesLastRowAndColumn()
        {
            // The tile's own chunks are missing; the tile east of it (chunk x = 4) and north (chunk z = 4) exist.
            var tile = FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, Step,
                coord => coord.X == 4 || coord.Z == 4 ? Plane(coord, 65) : null);

            Assert.That(tile.IsEmpty, Is.True);
            Assert.That(tile[32, 5], Is.EqualTo(Expected(256f, 40f)).Within(1e-3f));
            Assert.That(tile[5, 32], Is.EqualTo(Expected(40f, 256f)).Within(1e-3f));
            Assert.That(tile[32, 32], Is.EqualTo(Expected(256f, 256f)).Within(1e-3f));
            Assert.That(tile[5, 5], Is.EqualTo(0f));
        }

        [Test]
        public void Sample_AsksForEachChunkOnce_PlusTheEdgeNeighbours()
        {
            var asked = new List<ChunkCoord>();

            FarTiles.Sample(new ChunkCoord(2, 3), ChunksPerTile, Step, coord => { asked.Add(coord); return null; });

            Assert.That(asked, Is.Unique);
            Assert.That(asked, Has.Count.EqualTo((ChunksPerTile + 1) * (ChunksPerTile + 1)));
            Assert.That(asked, Has.Member(new ChunkCoord(8, 12)).And.Member(new ChunkCoord(12, 16)));
            Assert.That(asked, Has.No.Member(new ChunkCoord(7, 12)));
        }

        [Test]
        public void Sample_NeighbouringTiles_ShareTheirEdgeSamples()
        {
            Func<ChunkCoord, ChunkHeights> land = coord => Plane(coord, 65);
            var west = FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, Step, land);
            var east = FarTiles.Sample(new ChunkCoord(1, 0), ChunksPerTile, Step, land);

            for (var z = 0; z < west.Resolution; z++)
                Assert.That(east[0, z], Is.EqualTo(west[west.Resolution - 1, z]));
        }

        [Test]
        public void Sample_RecordsTheGroundShowingMost_UnpaintedIsNull()
        {
            var grass = Forge.Data.Records.GroundLayerRecord.IdFor("core", "grass");
            var rock = Forge.Data.Records.GroundLayerRecord.IdFor("core", "rock");
            // Chunk (0, 0) is rock on its east half, grass on its west; the others are unpainted.
            var weights = new float[2 * GroundPaint.TexelCount];
            for (var t = 0; t < GroundPaint.TexelCount; t++)
            {
                var east = t % GroundPaint.Resolution >= 32;
                weights[t] = east ? 0f : 1f;
                weights[GroundPaint.TexelCount + t] = east ? 1f : 0f;
            }

            var paint = GroundPaint.FromWeights(new[] { grass, rock }, weights);
            var tile = FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, Step, coord => Plane(coord, 65),
                coord => coord == new ChunkCoord(0, 0) ? paint : null);

            Assert.That(tile.GroundAt(1, 3), Is.EqualTo(grass), "8 m in: the west half.");
            Assert.That(tile.GroundAt(5, 3), Is.EqualTo(rock), "40 m in: the east half.");
            Assert.That(tile.GroundAt(9, 3), Is.Null, "72 m in: chunk (1, 0), unpainted.");
            Assert.That(tile.GroundAt(8, 3), Is.EqualTo(rock), "On the edge the two chunks share, the painted one's ground shows.");
            Assert.That(FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, Step, coord => Plane(coord, 65)).GroundAt(5, 3), Is.Null,
                "Without paint, nothing is recorded.");
        }

        [Test]
        public void Sample_RejectsStepsThatDontFitTheSamples()
        {
            Func<ChunkCoord, ChunkHeights> land = coord => Plane(coord, 33);

            Assert.Throws<ArgumentOutOfRangeException>(() => FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, 3f, land));
            Assert.Throws<ArgumentOutOfRangeException>(() => FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, 0f, land));
            Assert.Throws<ArgumentOutOfRangeException>(() => FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, 1f, land)); // finer than the 2 m samples
            Assert.Throws<ArgumentOutOfRangeException>(() => FarTiles.Sample(new ChunkCoord(0, 0), 0, Step, land));
            Assert.Throws<ArgumentNullException>(() => FarTiles.Sample(new ChunkCoord(0, 0), ChunksPerTile, Step, null));
            Assert.DoesNotThrow(() => FarTiles.Sample(new ChunkCoord(0, 0), 1, 64f, land));
        }

        // A chunk whose height is x + 2z in world meters (kept well inside the height range).
        static HeightField Plane(ChunkCoord coord, int resolution)
        {
            var spacing = WorldGrid.SpacingOf(resolution);
            return HeightField.FromFunction(
                (x, z) => Expected(coord.X * WorldGrid.ChunkSize + x * spacing, coord.Z * WorldGrid.ChunkSize + z * spacing),
                resolution);
        }

        static float Expected(float worldX, float worldZ) => 100f + (worldX + 2f * worldZ) / 20f;
    }
}
