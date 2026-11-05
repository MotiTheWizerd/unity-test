using System;
using System.Linq;
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
    public class PaintStrokeTests
    {
        static readonly RecordId Grass = GroundLayerRecord.IdFor("core", "grass");
        static readonly RecordId Rock = GroundLayerRecord.IdFor("core", "rock");
        static readonly RecordId Sand = GroundLayerRecord.IdFor("core", "sand");

        static TerrainChunkRecord Chunk(int x, int z, ChunkPaint paint = null) =>
            new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(x, z)), Space, new ChunkCoord(x, z), HeightField.Flat(10f, 17), paint);

        // Four unpainted chunks meeting at the world origin, and the core ground layers.
        static EditSession NewSession(params Record[] extra) =>
            new EditSession(
                new[]
                {
                    Plugin("core", new Record[]
                    {
                        new GroundLayerRecord(Grass, "Grass", "ground/grass"),
                        new GroundLayerRecord(Rock, "Rock", "ground/rock"),
                        new GroundLayerRecord(Sand, "Sand", "ground/sand"),
                        Chunk(-1, -1), Chunk(0, -1), Chunk(-1, 0), Chunk(0, 0),
                    }.Concat(extra).ToArray()),
                },
                Plugin("user", new[] { "core" }));

        static PaintStroke Stroke(EditSession session, RecordId layer, float radius = 4f, float strength = 1f) =>
            new PaintStroke(session.World.Records, Space, layer, new BrushShape(radius, strength), Grass);

        [Test]
        public void Dab_PaintsTheLayerUnderTheBrush_AndUnpaintedGroundBecomesTheBaseLayer()
        {
            var session = NewSession();
            var stroke = Stroke(session, Rock);

            var changed = stroke.ApplyDab(new Vector2(20.5f, 20.5f));

            Assert.That(changed, Is.EquivalentTo(new[] { new ChunkCoord(0, 0) }));
            var paint = stroke.PaintOf(new ChunkCoord(0, 0)).Load();
            Assert.That(paint.Layers, Is.EqualTo(new[] { Grass, Rock }));
            Assert.That(paint[paint.IndexOf(Rock), 20, 20], Is.EqualTo(GroundPaint.Full), "Full strength at the brush's center.");
            Assert.That(paint[paint.IndexOf(Grass), 30, 30], Is.EqualTo(GroundPaint.Full), "Outside the brush the ground stays as it was.");
            Assert.That(paint[paint.IndexOf(Rock), 22, 20], Is.InRange(1, 254), "Soft toward the rim.");
            Assert.That(stroke.PaintOf(new ChunkCoord(-1, -1)), Is.Null, "Untouched chunks keep their paint.");
        }

        [Test]
        public void Dab_AcrossAChunkEdge_PaintsBothSides()
        {
            var stroke = Stroke(NewSession(), Sand, radius: 6f);

            var changed = stroke.ApplyDab(new Vector2(0f, 10f));

            Assert.That(changed, Is.EquivalentTo(new[] { new ChunkCoord(-1, 0), new ChunkCoord(0, 0) }));
            var west = stroke.PaintOf(new ChunkCoord(-1, 0)).Load();
            var east = stroke.PaintOf(new ChunkCoord(0, 0)).Load();
            Assert.That(west[west.IndexOf(Sand), 63, 10], Is.EqualTo(east[east.IndexOf(Sand), 0, 10]), "The texels either side of the edge match.");
        }

        [Test]
        public void Dab_OffTheLand_PaintsNothing()
        {
            var stroke = Stroke(NewSession(), Rock);

            Assert.That(stroke.ApplyDab(new Vector2(500f, 500f)), Is.Empty);
            Assert.That(stroke.ChangedRecords(), Is.Empty);
        }

        [Test]
        public void WeakDabs_BuildUp()
        {
            var stroke = Stroke(NewSession(), Rock, strength: 0.25f);
            var center = new Vector2(20.5f, 20.5f);

            stroke.ApplyDab(center);
            var once = stroke.PaintOf(new ChunkCoord(0, 0)).Load();
            stroke.ApplyDab(center);
            var twice = stroke.PaintOf(new ChunkCoord(0, 0)).Load();

            Assert.That(once[once.IndexOf(Rock), 20, 20], Is.EqualTo(64).Within(1));
            Assert.That(twice[twice.IndexOf(Rock), 20, 20], Is.EqualTo(112).Within(1), "1 − 0.75² of the texel.");
        }

        [Test]
        public void PaintingPaintedGround_KeepsItsOtherLayers()
        {
            var session = NewSession();
            var first = Stroke(session, Rock, radius: 10f);
            first.ApplyDab(new Vector2(20.5f, 20.5f));
            TerrainEdits.Commit(session, first);

            var second = Stroke(session, Sand, radius: 3f);
            second.ApplyDab(new Vector2(40f, 40f));

            var paint = second.PaintOf(new ChunkCoord(0, 0)).Load();
            Assert.That(paint.Layers, Is.EquivalentTo(new[] { Grass, Rock, Sand }));
            Assert.That(paint[paint.IndexOf(Rock), 20, 20], Is.EqualTo(GroundPaint.Full));
        }

        [Test]
        public void Commit_WritesThePaint_KeepsTheHeights_AsOneUndoStep()
        {
            var session = NewSession();
            var heights = session.World.Records.Get<TerrainChunkRecord>(Chunk(0, 0).Id).Heights;
            var stroke = Stroke(session, Rock, radius: 6f);
            stroke.ApplyDab(new Vector2(0f, 0f));

            var written = TerrainEdits.Commit(session, stroke);

            Assert.That(written, Is.EqualTo(4), "The dab sat on the corner of all four chunks.");
            var chunk = session.World.Records.Get<TerrainChunkRecord>(Chunk(0, 0).Id);
            Assert.That(chunk.Heights, Is.SameAs(heights));
            Assert.That(chunk.Paint.Layers, Has.Member(Rock));
            Assert.That(session.UndoDescription, Is.EqualTo("Paint (Rock)"));

            session.Undo();
            Assert.That(session.World.Records.Get<TerrainChunkRecord>(Chunk(0, 0).Id).Paint, Is.Null);
        }

        [Test]
        public void Stroke_NeedsAGroundLayer()
        {
            var session = NewSession();

            Assert.Throws<ArgumentException>(() => Stroke(session, GroundLayerRecord.IdFor("core", "lava")));
            Assert.Throws<ArgumentException>(() => new PaintStroke(session.World.Records, Space, Rock, new BrushShape(4f, 1f), default));
        }

        [Test]
        public void GroundLayers_AreListedByName_AndFoundByTheirLook()
        {
            var store = NewSession().World.Records;

            Assert.That(GroundLayers.Of(store).Select(l => l.DisplayName), Is.EqualTo(new[] { "Grass", "Rock", "Sand" }));
            Assert.That(GroundLayers.WithLook(store, "ground/rock"), Is.EqualTo(Rock));
            Assert.That(GroundLayers.WithLook(store, "ground/lava"), Is.Null);
        }
    }
}
