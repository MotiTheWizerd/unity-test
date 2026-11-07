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
        public void AnEvenSlope_PaintsInPatches_NotOneBand()
        {
            var store = new EditSession(new[] { Plugin("core", Chunk(0, Incline(20f))) }, Plugin("user", new[] { "core" })).World.Records;
            int Spread(AutoPaintRules rules)
            {
                var paint = AutoPaint.Paint(store, Space, rules, null).Single().Paint.Load();
                var dirt = Enumerable.Range(0, 64).Select(z => (int)paint[paint.IndexOf(Dirt), 30, z]).ToList();
                return dirt.Max() - dirt.Min();
            }

            Assert.That(Spread(new AutoPaintRules(Grass, slope: Dirt, steep: Rock) { SlopeWander = 0f }), Is.LessThanOrEqualTo(2), "Exact thresholds: one even share.");
            Assert.That(Spread(Rules), Is.GreaterThan(60), "Wandering thresholds: patches.");
        }

        [Test]
        public void EmptyRoles_FallToTheFlatGround()
        {
            var store = new EditSession(new[] { Plugin("core", Chunk(0, Incline(45f))) }, Plugin("user", new[] { "core" })).World.Records;

            var paint = AutoPaint.Paint(store, Space, new AutoPaintRules(Grass), null).Single().Paint.Load();

            Assert.That(paint.Layers, Is.EqualTo(new[] { Grass }));
        }

        static readonly RecordId Snow = GroundLayerRecord.IdFor("core", "snow");

        static GroundPaint SnowyPaintOf(TerrainChunkRecord chunk) =>
            AutoPaint.Paint(new EditSession(new[] { Plugin("core", chunk) }, Plugin("user", new[] { "core" })).World.Records, Space,
                    new AutoPaintRules(Grass, slope: Dirt, steep: Rock, shore: Sand, snow: Snow) { SnowLine = 58f }, null)
                .Single().Paint.Load();

        [Test]
        public void AboveTheSnowLine_IsSnow_BelowItTheLand()
        {
            Assert.That(SnowyPaintOf(Chunk(0, HeightField.Flat(75f))).DominantAt(30, 30), Is.EqualTo(Snow));
            Assert.That(SnowyPaintOf(Chunk(0, HeightField.Flat(40f))).Layers, Has.No.Member(Snow));
        }

        [Test]
        public void TheSnowLine_Blends_AndWanders()
        {
            var paint = SnowyPaintOf(Chunk(0, HeightField.Flat(58f)));
            var snow = paint.IndexOf(Snow);

            var shares = Enumerable.Range(0, 64).Select(x => (int)paint[snow, x, 30]).ToList();
            Assert.That(shares.Count(share => share > 0 && share < 255), Is.GreaterThan(48), "On the line, mostly part snow.");
            Assert.That(shares.Max() - shares.Min(), Is.GreaterThan(30), "The line wanders across level ground.");
        }

        [Test]
        public void Snow_HoldsOnSteepGround_UntilItIsTooSteep()
        {
            // 40° would be rock below the snow line, but snow holds on it; at 60° it slides off and the rock shows.
            Assert.That(SnowyPaintOf(Chunk(0, Incline(40f))).DominantAt(40, 30), Is.EqualTo(Snow));
            Assert.That(SnowyPaintOf(Chunk(0, Incline(60f))).DominantAt(40, 30), Is.EqualTo(Rock));
        }

        [Test]
        public void NoSnowLine_NoSnow()
        {
            var store = new EditSession(new[] { Plugin("core", Chunk(0, HeightField.Flat(150f))) }, Plugin("user", new[] { "core" })).World.Records;

            var paint = AutoPaint.Paint(store, Space, new AutoPaintRules(Grass, snow: Snow), null).Single().Paint.Load();

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
