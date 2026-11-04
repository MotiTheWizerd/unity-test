using System;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Water;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>Lakes over <see cref="PitTerrain"/>: the fill, the water map with lakes, and the lake edits.</summary>
    public class LakeTests
    {
        [Test]
        public void Survey_FindsTheGroundAndTheLowestPointOfTheRim()
        {
            var basin = LakeFill.Survey(PitTerrain.Grid(PitTerrain.Session()), PitTerrain.PitCenter).Value;

            Assert.That(basin.Ground, Is.EqualTo(PitTerrain.PitFloor));
            Assert.That(basin.Brim, Is.EqualTo(PitTerrain.Gutter), "The gutter is the way out.");
            Assert.That(basin.HoldsWater, Is.True);
        }

        [Test]
        public void Survey_OnFlatGround_HoldsNoWater_AndOffTheLand_IsNull()
        {
            var terrain = PitTerrain.Grid(PitTerrain.Session());

            Assert.That(LakeFill.Survey(terrain, new Vector2(20f, 20f)).Value.HoldsWater, Is.False);
            Assert.That(LakeFill.Survey(terrain, new Vector2(-50f, 20f)), Is.Null);
        }

        [Test]
        public void Fill_BelowTheBrim_FillsThePitOnly()
        {
            var extent = LakeFill.Fill(PitTerrain.Grid(PitTerrain.Session()), PitTerrain.PitCenter, 44f);

            Assert.That(extent.Spills, Is.False);
            Assert.That(extent.SampleCount, Is.EqualTo(PitTerrain.PitSamples));
            Assert.That(extent.Deepest, Is.EqualTo(4f));
            Assert.That((extent.MinX, extent.MaxX, extent.MinZ, extent.MaxZ), Is.EqualTo((54, 74, 54, 74)));
            Assert.That(extent.IsWet(80, 64), Is.False, "The gutter is above the water.");
            Assert.That(extent.CoversCell(74, 60), Is.True, "A cell with one wet corner is drawn over.");
        }

        [Test]
        public void CellRuns_CoverEveryCellWithAWetCorner_OneRunPerRow()
        {
            var extent = LakeFill.Fill(PitTerrain.Grid(PitTerrain.Session()), PitTerrain.PitCenter, 44f);

            var runs = extent.CellRuns().ToList();

            // Wet samples 54–74 make covered cells 53–74 (a cell's lower corner is one sample before a wet one).
            Assert.That(runs, Is.EqualTo(Enumerable.Range(53, 22).Select(z => (z, 53, 74))));
        }

        [Test]
        public void Fill_AboveTheBrim_RunsOffTheLand()
        {
            var extent = LakeFill.Fill(PitTerrain.Grid(PitTerrain.Session()), PitTerrain.PitCenter, 46f);

            Assert.That(extent.Spills, Is.True);
            Assert.That(extent.IsEmpty, Is.True);
        }

        [Test]
        public void Fill_AtOrBelowTheGround_IsDry()
        {
            var terrain = PitTerrain.Grid(PitTerrain.Session());

            Assert.That(LakeFill.Fill(terrain, PitTerrain.PitCenter, PitTerrain.PitFloor).IsEmpty, Is.True);
            Assert.That(LakeFill.Fill(terrain, PitTerrain.PitCenter, PitTerrain.PitFloor).Spills, Is.False);
        }

        [Test]
        public void WaterMap_ReadsTheLakes_AndTheHighestWaterWins()
        {
            var lake = new LakeRecord(RecordId.Parse("core:lake/1"), PitTerrain.Space, 64f, 64f, 44f);
            var session = PitTerrain.Session(lake);
            var water = WaterMap.Of(session.World.Records, PitTerrain.Grid(session));

            Assert.That(water.DepthAt(PitTerrain.PitCenter), Is.EqualTo(4f));
            Assert.That(water.SurfaceAt(new Vector2(20f, 20f)), Is.Null);
            Assert.That(water.LakeAt(new Vector2(60f, 60f)).Record.Id, Is.EqualTo(lake.Id));
            Assert.That(water.LakeAt(new Vector2(20f, 20f)), Is.Null);

            var flooded = new WaterMap(water.Terrain, 47f, water.Lakes);
            Assert.That(flooded.DepthAt(PitTerrain.PitCenter), Is.EqualTo(7f), "A sea above the lake covers it.");
        }

        [Test]
        public void WaterMap_ALakeThatSpills_HoldsNoWater()
        {
            var lake = new LakeRecord(RecordId.Parse("core:lake/1"), PitTerrain.Space, 64f, 64f, 46f);
            var session = PitTerrain.Session(lake);

            var water = WaterMap.Of(session.World.Records, PitTerrain.Grid(session));

            Assert.That(water.Lakes.Single().Extent.Spills, Is.True);
            Assert.That(water.DepthAt(PitTerrain.PitCenter), Is.EqualTo(0f));
        }

        [Test]
        public void AddLake_FillsTheHollow_AsOneUndoStep()
        {
            var session = PitTerrain.Session();

            var lake = WaterEdits.AddLake(session, PitTerrain.Space, new Vector2(60f, 62f), 44.5f);

            Assert.That(lake.Id.ToString(), Is.EqualTo("user:lake/000001"));
            Assert.That(session.World.Records.Get<LakeRecord>(lake.Id).Level, Is.EqualTo(44.5f));
            Assert.That(session.UndoDescription, Is.EqualTo("Add lake: 44.5 m, up to 4.5 m deep"));
            session.Undo();
            Assert.That(session.World.Records.OfType<LakeRecord>(), Is.Empty);
        }

        [Test]
        public void AddLake_ThatWouldSpill_IsRefused_SayingHowHighItCanGo()
        {
            var session = PitTerrain.Session();

            var spill = Assert.Throws<ArgumentException>(() => WaterEdits.AddLake(session, PitTerrain.Space, PitTerrain.PitCenter, 48f));
            var flat = Assert.Throws<ArgumentException>(() => WaterEdits.AddLake(session, PitTerrain.Space, new Vector2(20f, 20f), 52f));
            var low = Assert.Throws<ArgumentException>(() => WaterEdits.AddLake(session, PitTerrain.Space, PitTerrain.PitCenter, 39f));

            Assert.That(spill.Message, Does.Contain("only up to 45 m"));
            Assert.That(flat.Message, Does.Contain("isn't in a hollow"));
            Assert.That(low.Message, Does.Contain("40 m"));
            Assert.That(session.World.Records.OfType<LakeRecord>(), Is.Empty);
        }

        [Test]
        public void SetLakeLevel_AndRemoveLake_AreUndoable()
        {
            var session = PitTerrain.Session();
            var lake = WaterEdits.AddLake(session, PitTerrain.Space, PitTerrain.PitCenter, 42f);

            Assert.That(WaterEdits.SetLakeLevel(session, lake.Id, 44f), Is.True);
            Assert.That(WaterEdits.SetLakeLevel(session, lake.Id, 44f), Is.False);
            Assert.That(session.World.Records.Get<LakeRecord>(lake.Id).Level, Is.EqualTo(44f));
            Assert.Throws<ArgumentException>(() => WaterEdits.SetLakeLevel(session, lake.Id, 47f));

            WaterEdits.RemoveLake(session, lake.Id);
            Assert.That(session.World.Records.TryGet(lake.Id, out _), Is.False);
            Assert.That(session.UndoDescription, Is.EqualTo("Remove lake"));

            session.Undo();
            Assert.That(session.World.Records.Get<LakeRecord>(lake.Id).Level, Is.EqualTo(44f));
        }

        [Test]
        public void Lakes_AreRefusedInInteriors()
        {
            var session = new Forge.Editing.EditSession(
                new[] { Forge.Tests.Data.TestRecords.Plugin("core", PitTerrain.Records(SpaceKind.Interior)) },
                Forge.Tests.Data.TestRecords.Plugin("user", new[] { "core" }));

            Assert.Throws<ArgumentException>(() => WaterEdits.AddLake(session, PitTerrain.Space, PitTerrain.PitCenter, 44f));
        }
    }
}
