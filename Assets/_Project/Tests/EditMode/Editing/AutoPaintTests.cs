using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class AutoPaintTests
    {
        static readonly RecordId Grass = GroundLayerRecord.IdFor("core", "grass");
        static readonly RecordId Dirt = GroundLayerRecord.IdFor("core", "dirt");
        static readonly RecordId Rock = GroundLayerRecord.IdFor("core", "rock");
        static readonly RecordId Sand = GroundLayerRecord.IdFor("core", "sand");

        static readonly AutoPaintRules Rules = new AutoPaintRules(Grass, slope: Dirt, steep: Rock, shore: Sand);

        static TerrainChunkRecord Chunk(int x, HeightField heights) =>
            new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(x, 0)), Space, new ChunkCoord(x, 0), heights);

        // Height rising along X: `degrees` steep.
        static HeightField Incline(float degrees) =>
            HeightField.FromFunction((x, z) => 50f + x * (float)Math.Tan(degrees * Math.PI / 180.0), 65);

        static GroundPaint PaintOf(params TerrainChunkRecord[] chunks) =>
            AutoPaint.Paint(new EditSession(new[] { Plugin("core", chunks) }, Plugin("user", new[] { "core" })).World.Records, Space, Rules, null)
                .Single().Paint.Load();

        [Test]
        public void FlatLand_IsTheFlatGround_Only()
        {
            var paint = PaintOf(Chunk(0, HeightField.Flat(50f)));

            Assert.That(paint.Layers, Is.EqualTo(new[] { Grass }));
        }

        [TestCase(10f, "grass")]
        [TestCase(27f, "dirt")]
        [TestCase(45f, "rock")]
        public void Slopes_GetTheirGround(float degrees, string expected)
        {
            var paint = PaintOf(Chunk(0, Incline(degrees)));

            Assert.That(paint.DominantAt(30, 30), Is.EqualTo(GroundLayerRecord.IdFor("core", expected)));
        }

        [Test]
        public void TheShore_JustAboveTheSea_IsSand_AndBlendsIntoTheLand()
        {
            // Gently rising from 49 m to 53 m across the chunk; the sea at 49 m.
            var chunk = Chunk(0, HeightField.FromFunction((x, z) => 49f + x / 16f, 65));
            var store = new EditSession(new[] { Plugin("core", chunk) }, Plugin("user", new[] { "core" })).World.Records;

            var paint = AutoPaint.Paint(store, Space, Rules, seaLevel: 49f).Single().Paint.Load();

            Assert.That(paint.DominantAt(2, 30), Is.EqualTo(Sand), "Half a meter above the sea.");
            Assert.That(paint.DominantAt(60, 30), Is.EqualTo(Grass), "Nearly four meters up.");
            Assert.That(paint[paint.IndexOf(Sand), 24, 30], Is.InRange(1, 254), "The shore blends.");
        }

        [Test]
        public void EmptyRoles_FallToTheFlatGround()
        {
            var store = new EditSession(new[] { Plugin("core", Chunk(0, Incline(45f))) }, Plugin("user", new[] { "core" })).World.Records;

            var paint = AutoPaint.Paint(store, Space, new AutoPaintRules(Grass), null).Single().Paint.Load();

            Assert.That(paint.Layers, Is.EqualTo(new[] { Grass }));
        }

        [Test]
        public void Apply_RepaintsEveryChunk_KeepsHeights_AsOneUndoStep()
        {
            var session = new EditSession(new[] { Plugin("core", Chunk(0, Incline(45f)), Chunk(1, HeightField.Flat(50f))) }, Plugin("user", new[] { "core" }));
            var heights = session.World.Records.Get<TerrainChunkRecord>(Chunk(0, Incline(1f)).Id).Heights;

            var painted = AutoPaint.Apply(session, Space, Rules);

            Assert.That(painted, Is.EqualTo(2));
            Assert.That(session.UndoDescription, Is.EqualTo("Auto paint"));
            var chunk = session.World.Records.Get<TerrainChunkRecord>(Chunk(0, Incline(1f)).Id);
            Assert.That(chunk.Heights, Is.SameAs(heights));
            Assert.That(chunk.Paint.Layers, Has.Member(Rock));

            session.Undo();
            Assert.That(session.World.Records.Get<TerrainChunkRecord>(Chunk(0, Incline(1f)).Id).Paint, Is.Null);
        }
    }
}
