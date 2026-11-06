using System;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Geometry;
using Forge.Editing.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class GroundPaintingTests
    {
        static readonly RecordId Grass = GroundLayerRecord.IdFor("core", "grass");
        static readonly RecordId Rock = GroundLayerRecord.IdFor("core", "rock");

        // A full texel, as the int the reads below return (tuples of byte and int never compare equal).
        const int Full = GroundPaint.Full;

        static TerrainChunkRecord Chunk(int x, int z) =>
            new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(x, z)), Space, new ChunkCoord(x, z), HeightField.Flat(10f, 17));

        // Four unpainted chunks meeting at the world origin, and two ground layers.
        static EditSession NewSession() =>
            new EditSession(
                new[]
                {
                    Plugin("core", new Record[]
                    {
                        new GroundLayerRecord(Grass, "Grass", "ground/grass"),
                        new GroundLayerRecord(Rock, "Rock", "ground/rock"),
                        Chunk(-1, -1), Chunk(0, -1), Chunk(-1, 0), Chunk(0, 0),
                    }),
                },
                Plugin("user", new[] { "core" }));

        static GroundPainting Painting(EditSession session) => new GroundPainting(session.World.Records, Space, Rock, Grass);

        // How much of world texel (gx, gz) is rock, 0..255; texels are 1 m, so texel (gx, gz) is centered on (gx + 0.5, gz + 0.5).
        static int RockAt(GroundPainting painting, int gx, int gz)
        {
            var coord = new ChunkCoord((int)Math.Floor(gx / 64.0), (int)Math.Floor(gz / 64.0));
            var paint = painting.PaintOf(coord)?.Load();
            var layer = paint?.IndexOf(Rock) ?? -1;
            return layer < 0 ? 0 : paint[layer, gx - coord.X * 64, gz - coord.Z * 64];
        }

        static Vector2[] Points(params (float X, float Z)[] points) => points.Select(p => new Vector2(p.X, p.Z)).ToArray();

        [Test]
        public void APath_WithAHardEdge_PaintsABandExactlyItsWidth()
        {
            var painting = Painting(NewSession());

            var painted = painting.PaintPath(Points((2f, 10f), (30f, 10f)), width: 4f, edge: 0f, strength: 1f);

            // Texel rows 8-11 are centered within 2 m of z = 10; rows 7 and 12 are 2.5 m away.
            Assert.That(Enumerable.Range(8, 4).Select(z => RockAt(painting, 15, z)), Is.All.EqualTo(GroundPaint.Full));
            Assert.That(RockAt(painting, 15, 7), Is.Zero);
            Assert.That(RockAt(painting, 15, 12), Is.Zero);
            Assert.That(painted, Is.EqualTo(painting.PaintedTexels));
        }

        [Test]
        public void ASoftEdge_IsCenteredOnTheSide_SoTheBandKeepsItsWidth()
        {
            var painting = Painting(NewSession());

            // The band's sides run along z = 8.5 and z = 12.5, through texel centers.
            painting.PaintPath(Points((2f, 10.5f), (30f, 10.5f)), width: 4f, edge: 2f, strength: 1f);

            Assert.That(RockAt(painting, 15, 10), Is.EqualTo(GroundPaint.Full), "The middle.");
            Assert.That(RockAt(painting, 15, 11), Is.EqualTo(GroundPaint.Full), "Half an edge inside the side: still full.");
            Assert.That(RockAt(painting, 15, 12), Is.InRange(110, 145), "On the side: half.");
            Assert.That(RockAt(painting, 15, 13), Is.Zero, "Half an edge outside: nothing.");
        }

        [Test]
        public void ARectangle_TurnsWithItsHeading()
        {
            var facingNorth = Painting(NewSession());
            facingNorth.PaintRect(new GroundRect(new Vector2(20f, 20f), 10f, 4f, 0f), 0f, 1f);
            var facingEast = Painting(NewSession());
            facingEast.PaintRect(new GroundRect(new Vector2(20f, 20f), 10f, 4f, 90f), 0f, 1f);

            // Facing north the width runs east-west; facing east it runs north-south.
            Assert.That((RockAt(facingNorth, 24, 20), RockAt(facingNorth, 20, 24)), Is.EqualTo((Full, 0)));
            Assert.That((RockAt(facingEast, 24, 20), RockAt(facingEast, 20, 24)), Is.EqualTo((0, Full)));
        }

        [Test]
        public void SoftEdgedAreas_AreFullInside_AndHalfOnTheirOutline()
        {
            // The same 10 m square twice, its sides along x and z = 15.5 and 25.5, through texel centers.
            var rect = Painting(NewSession());
            rect.PaintRect(new GroundRect(new Vector2(20.5f, 20.5f), 10f, 10f), edge: 2f, strength: 1f);
            var polygon = Painting(NewSession());
            polygon.PaintPolygon(Points((15.5f, 15.5f), (25.5f, 15.5f), (25.5f, 25.5f), (15.5f, 25.5f)), edge: 2f, strength: 1f);

            foreach (var painting in new[] { rect, polygon })
            {
                Assert.That(RockAt(painting, 20, 20), Is.EqualTo(GroundPaint.Full), "The middle.");
                Assert.That(RockAt(painting, 24, 20), Is.EqualTo(GroundPaint.Full), "Half an edge inside.");
                Assert.That(RockAt(painting, 25, 20), Is.InRange(110, 145), "On the outline: half.");
                Assert.That(RockAt(painting, 26, 20), Is.Zero, "Half an edge outside.");
            }
        }

        [Test]
        public void APolygon_PaintsWhatItEncloses_NotItsNotch()
        {
            var painting = Painting(NewSession());

            // An L: a 20 m square without its north-east quarter.
            painting.PaintPolygon(Points((0f, 0f), (20f, 0f), (20f, 10f), (10f, 10f), (10f, 20f), (0f, 20f)), 0f, 1f);

            Assert.That(RockAt(painting, 5, 15), Is.EqualTo(GroundPaint.Full));
            Assert.That(RockAt(painting, 15, 5), Is.EqualTo(GroundPaint.Full));
            Assert.That(RockAt(painting, 15, 15), Is.Zero, "The notch.");
            Assert.That(RockAt(painting, 25, 5), Is.Zero);
        }

        [Test]
        public void Strength_LetsTheOldGroundShow_AndAShapePaintsEachTexelOnce()
        {
            var painting = Painting(NewSession());

            // There and back along the same line: the band overlaps itself, but a shape paints each texel once.
            painting.PaintPath(Points((2f, 10f), (30f, 10f), (2f, 10f)), width: 4f, edge: 0f, strength: 0.5f);

            Assert.That(RockAt(painting, 15, 10), Is.InRange(125, 130));
        }

        [Test]
        public void Shapes_CrossChunkEdges_AndPaintNothingOffTheLand()
        {
            var painting = Painting(NewSession());

            painting.PaintPath(Points((-10f, 10f), (10f, 10f)), width: 4f, edge: 0f, strength: 1f);

            Assert.That(painting.TouchedChunks, Is.EquivalentTo(new[] { new ChunkCoord(-1, 0), new ChunkCoord(0, 0) }));
            Assert.That((RockAt(painting, -1, 10), RockAt(painting, 0, 10)), Is.EqualTo((Full, Full)));

            var offTheLand = Painting(NewSession());
            Assert.That(offTheLand.PaintRect(new GroundRect(new Vector2(500f, 500f), 10f, 10f), 1f, 1f), Is.Zero);
            Assert.That(offTheLand.ChangedRecords(), Is.Empty);
        }

        [Test]
        public void ShapesWiderThanTheSpan_AreRefused()
        {
            Assert.Throws<ArgumentException>(() => Painting(NewSession()).PaintRect(new GroundRect(Vector2.Zero, 300f, 10f), 0f, 1f));
        }

        [Test]
        public void Commit_WritesTheShapes_AsOneUndoStep()
        {
            var session = NewSession();
            var painting = Painting(session);
            painting.PaintPath(Points((2f, 10f), (30f, 10f)), 4f, 1f, 1f);
            painting.PaintRect(new GroundRect(new Vector2(-20f, -20f), 6f, 6f), 1f, 1f);

            var chunks = TerrainEdits.Commit(session, painting);

            Assert.That(chunks, Is.EqualTo(2));
            Assert.That(session.UndoDescription, Is.EqualTo("Paint (Rock)"));
            session.Undo();
            Assert.That(new TerrainGrid(session.World.Records, Space).TryGetChunk(new ChunkCoord(0, 0), out var chunk) ? chunk.Paint : null, Is.Null,
                "Undo takes the paint away.");
        }
    }
}
