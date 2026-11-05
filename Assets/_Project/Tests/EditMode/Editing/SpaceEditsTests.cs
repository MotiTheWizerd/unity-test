using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Spaces;
using Forge.Editing.Terrain;
using Forge.Tests.Data;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class SpaceEditsTests
    {
        static EditSession NewSession() =>
            new EditSession(new[] { Plugin("core", Array.Empty<Record>()) }, Plugin("user", new[] { "core" }));

        [Test]
        public void CreateWorldspace_LaysFlatLandCenteredOnTheOrigin_AsOneUndoStep()
        {
            var session = NewSession();

            var id = SpaceEdits.CreateWorldspace(session, "Whiterun", 3, 40f);

            Assert.That(id.ToString(), Is.EqualTo("user:space/whiterun"));
            var space = session.World.Records.Get<SpaceRecord>(id);
            Assert.That((space.DisplayName, space.Kind, space.BaseHeight), Is.EqualTo(("Whiterun", SpaceKind.Worldspace, 40f)));

            var chunks = session.World.Records.OfType<TerrainChunkRecord>().Where(c => c.Space == id).ToList();
            Assert.That(chunks.Select(c => c.Coord), Is.EquivalentTo(
                from z in new[] { -1, 0, 1 } from x in new[] { -1, 0, 1 } select new ChunkCoord(x, z)));
            Assert.That(chunks.All(c => c.Heights.Load().ToArray().All(h => h == 40f)), Is.True);

            Assert.That(session.UndoDescription, Is.EqualTo("New worldspace Whiterun"));
            session.Undo();
            Assert.That(session.World.Records.TryGet(id, out _), Is.False);
            Assert.That(session.World.Records.OfType<TerrainChunkRecord>(), Is.Empty);
        }

        [Test]
        public void AnEvenSize_StaysCenteredOnTheOrigin()
        {
            var session = NewSession();

            var id = SpaceEdits.CreateWorldspace(session, "Wide", 8, 50f);

            var coords = session.World.Records.OfType<TerrainChunkRecord>().Where(c => c.Space == id).Select(c => c.Coord).ToList();
            Assert.That(coords.Count, Is.EqualTo(64));
            Assert.That((coords.Min(c => c.X), coords.Max(c => c.X)), Is.EqualTo((-4, 3)));
        }

        [Test]
        public void CreateInterior_HasNoLand()
        {
            var session = NewSession();

            var id = SpaceEdits.CreateInterior(session, "Bannered Mare");

            Assert.That(session.World.Records.Get<SpaceRecord>(id).Kind, Is.EqualTo(SpaceKind.Interior));
            Assert.That(session.World.Records.OfType<TerrainChunkRecord>(), Is.Empty);
            Assert.That(session.UndoDescription, Is.EqualTo("New interior Bannered Mare"));
        }

        [Test]
        public void ATakenName_GetsANumber()
        {
            var session = NewSession();
            SpaceEdits.CreateInterior(session, "Cave");

            var second = SpaceEdits.CreateInterior(session, "cave!");

            Assert.That(second.Name, Is.EqualTo("space/cave_2"));
        }

        [TestCase("Whiterun Hall", "whiterun_hall")]
        [TestCase("  Dragonsreach -- Jarl's Room ", "dragonsreach_jarl_s_room")]
        [TestCase("???", "space")]
        public void Slug_MakesAValidName(string displayName, string expected)
        {
            Assert.That(SpaceEdits.Slug(displayName), Is.EqualTo(expected));
        }

        [Test]
        public void SetWaterLevel_SetsAndRemovesTheSea_EachAsOneUndoStep()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Coast", 3, 50f);

            Assert.That(SpaceEdits.SetWaterLevel(session, id, 48f), Is.True);
            Assert.That(session.World.Records.Get<SpaceRecord>(id).WaterLevel, Is.EqualTo(48f));
            Assert.That(session.UndoDescription, Is.EqualTo("Sea level of Coast: 48 m"));

            Assert.That(SpaceEdits.SetWaterLevel(session, id, null), Is.True);
            Assert.That(session.World.Records.Get<SpaceRecord>(id).WaterLevel, Is.Null);
            Assert.That(session.UndoDescription, Is.EqualTo("Remove the sea of Coast"));

            session.Undo();
            Assert.That(session.World.Records.Get<SpaceRecord>(id).WaterLevel, Is.EqualTo(48f));
        }

        [Test]
        public void SetWaterLevel_ToTheSameLevel_ChangesNothing()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Coast", 3, 50f);
            SpaceEdits.SetWaterLevel(session, id, 48f);
            var world = session.World;

            Assert.That(SpaceEdits.SetWaterLevel(session, id, 48f), Is.False);
            Assert.That(session.World, Is.SameAs(world));
        }

        [Test]
        public void SetWaterLevel_OnAMastersSpace_OverridesItInTheActivePlugin()
        {
            var session = new EditSession(new[] { Plugin("core", new SpaceRecord(Space, "Tamriel", SpaceKind.Worldspace, 50f)) }, Plugin("user", new[] { "core" }));

            SpaceEdits.SetWaterLevel(session, Space, 40f);

            var space = session.World.Records.Get<SpaceRecord>(Space);
            Assert.That((space.DisplayName, space.BaseHeight, space.WaterLevel), Is.EqualTo(("Tamriel", 50f, (float?)40f)));
            Assert.That(session.BuildActivePlugin().Records.OfType<SpaceRecord>().Single().Id, Is.EqualTo(Space));
        }

        [Test]
        public void SetWaterLevel_IsRefusedForInteriorsAndMissingSpaces()
        {
            var session = NewSession();
            var interior = SpaceEdits.CreateInterior(session, "Cellar");

            Assert.Throws<ArgumentException>(() => SpaceEdits.SetWaterLevel(session, interior, 1f));
            Assert.Throws<ArgumentException>(() => SpaceEdits.SetWaterLevel(session, OtherSpace, 1f));
        }

        [Test]
        public void SetClimate_OnAMastersSpace_OverridesIt_AsOneUndoStep_KeepingTheSea()
        {
            var climate = ClimateTests.Northern();
            var session = new EditSession(
                new[] { Plugin("core", climate, new SpaceRecord(Space, "Tamriel", SpaceKind.Worldspace, 50f, 45f)) },
                Plugin("user", new[] { "core" }));

            Assert.That(SpaceEdits.SetClimate(session, Space, climate.Id), Is.True);
            var space = session.World.Records.Get<SpaceRecord>(Space);
            Assert.That((space.Climate, space.WaterLevel), Is.EqualTo(((RecordId?)climate.Id, (float?)45f)));
            Assert.That(session.UndoDescription, Is.EqualTo("Climate of Tamriel: Northern"));
            Assert.That(SpaceEdits.SetClimate(session, Space, climate.Id), Is.False, "Already so.");

            session.Undo();
            Assert.That(session.World.Records.Get<SpaceRecord>(Space).Climate, Is.Null);
        }

        [Test]
        public void SetClimate_IsRefusedForInteriors_AndClimatesThatDoNotExist()
        {
            var session = NewSession();
            var interior = SpaceEdits.CreateInterior(session, "Cellar");
            var land = SpaceEdits.CreateWorldspace(session, "Land", 3, 50f);

            Assert.Throws<ArgumentException>(() => SpaceEdits.SetClimate(session, interior, null));
            Assert.Throws<ArgumentException>(() => SpaceEdits.SetClimate(session, land, ClimateRecord.IdFor("core", "nowhere")));
        }

        [Test]
        public void CreateWorldspace_At2mSpacing_MakesCoarserChunks_AndExtendLandFollows()
        {
            var session = NewSession();

            var id = SpaceEdits.CreateWorldspace(session, "Plains", 3, 50f, spacing: 2f);
            SpaceEdits.ExtendLand(session, id, LandSide.North, 1);

            var space = session.World.Records.Get<SpaceRecord>(id);
            Assert.That((space.HeightmapResolution, space.SampleSpacing), Is.EqualTo((33, 2f)));
            var chunks = session.World.Records.OfType<TerrainChunkRecord>().Where(c => c.Space == id).ToList();
            Assert.That(chunks.Count, Is.EqualTo(12));
            Assert.That(chunks.Select(c => c.Heights.Resolution).Distinct(), Is.EqualTo(new[] { 33 }));
            Assert.That(new TerrainGrid(session.World.Records, id).Spacing, Is.EqualTo(2f));
        }

        [Test]
        public void CreateWorldspace_RefusesASpacingThatDoesNotFitTheChunk()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpaceEdits.CreateWorldspace(NewSession(), "Odd", 3, 50f, spacing: 3f));
        }

        [Test]
        public void ExtendLand_AddsAStripAlongTheWholeSide_AsOneUndoStep()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Land", 3, 50f);

            var added = SpaceEdits.ExtendLand(session, id, LandSide.East, 2);

            Assert.That(added, Is.EqualTo(6));
            var bounds = LandBounds.Of(session.World.Records, id).Value;
            Assert.That((bounds.MinX, bounds.MaxX, bounds.MinZ, bounds.MaxZ), Is.EqualTo((-1, 3, -1, 1)));
            Assert.That(bounds.ToString(), Is.EqualTo("5×3 chunks (320 × 192 m)"));
            Assert.That(session.UndoDescription, Is.EqualTo("Extend Land 2 chunks east"));

            session.Undo();
            Assert.That(LandBounds.Of(session.World.Records, id).Value.Width, Is.EqualTo(3));
        }

        [Test]
        public void ExtendLand_RampsFromTheExistingEdge_ToTheBaseHeight()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Hills", 1, 50f);
            // Raise the land's north edge to 60 m: the new chunk north of it must start at 60 and ease back to 50.
            var chunk = session.World.Records.OfType<TerrainChunkRecord>().Single();
            var n = chunk.Heights.Resolution;
            session.Put(chunk.WithHeights(HeightField.FromFunction((x, z) => z == n - 1 ? 60f : 50f, n)));

            SpaceEdits.ExtendLand(session, id, LandSide.North, 1);

            var north = session.World.Records.OfType<TerrainChunkRecord>().Single(c => c.Coord == new ChunkCoord(0, 1)).Heights.Load();
            Assert.That(north[3, 0], Is.EqualTo(60f), "Shares the edge exactly.");
            Assert.That(north[3, (n - 1) / 2], Is.EqualTo(55f).Within(1e-4f), "Halfway across, halfway down.");
            Assert.That(north[3, n - 1], Is.EqualTo(50f), "The far edge is at the base height.");
        }

        [Test]
        public void ExtendLand_KeepsChunksAlreadyThere_AndFillsUnevenLand()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Land", 3, 50f);
            // A lone chunk sticks out east at z = 0 (as a mod's land might), so the land's reach is already 4 wide.
            var lone = new ChunkCoord(2, 0);
            session.Put(new TerrainChunkRecord(TerrainChunkRecord.IdFor("user", id, lone), id, lone, HeightField.Flat(60f)));

            var added = SpaceEdits.ExtendLand(session, id, LandSide.East, 1);

            // The strip goes beyond the land's reach (x = 3); the gaps beside the lone chunk were there and stay.
            Assert.That(added, Is.EqualTo(3));
            Assert.That(LandBounds.Of(session.World.Records, id).Value.ToString(), Is.EqualTo("5×3 chunks (320 × 192 m)"));
            Assert.That(session.World.Records.OfType<TerrainChunkRecord>().Any(c => c.Coord == new ChunkCoord(2, 1)), Is.False);
            var kept = session.World.Records.OfType<TerrainChunkRecord>().Single(c => c.Coord == lone).Heights.Load();
            Assert.That(kept[0, 0], Is.EqualTo(60f), "The chunk that was there is untouched.");
        }

        [Test]
        public void ExtendLand_IsRefusedForInteriors_BadCounts_AndBeyondTheReach()
        {
            var session = NewSession();
            var interior = SpaceEdits.CreateInterior(session, "Cellar");
            var land = SpaceEdits.CreateWorldspace(session, "Land", 3, 50f);

            Assert.Throws<ArgumentException>(() => SpaceEdits.ExtendLand(session, interior, LandSide.North, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpaceEdits.ExtendLand(session, land, LandSide.North, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpaceEdits.ExtendLand(session, land, LandSide.North, SpaceEdits.MaxExtendBy + 1));
        }

        [Test]
        public void ExtendLand_OnEverySide_AddsAFullRing_CornersIncluded_AsOneUndoStep()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Land", 3, 50f);

            var added = SpaceEdits.ExtendLand(session, id, LandSides.All, 2);

            // 3×3 grows to 7×7: every coordinate of the new rectangle is land, corners included.
            Assert.That(added, Is.EqualTo(49 - 9));
            var bounds = LandBounds.Of(session.World.Records, id).Value;
            Assert.That((bounds.MinX, bounds.MaxX, bounds.MinZ, bounds.MaxZ), Is.EqualTo((-3, 3, -3, 3)));
            Assert.That(session.World.Records.OfType<TerrainChunkRecord>().Count(c => c.Space == id), Is.EqualTo(49));
            Assert.That(session.UndoDescription, Is.EqualTo("Extend Land 2 chunks on every side"));

            session.Undo();
            Assert.That(session.World.Records.OfType<TerrainChunkRecord>().Count(c => c.Space == id), Is.EqualTo(9));
        }

        [Test]
        public void ExtendLand_OnEverySide_CornersRampFromTheStripsBesideThem()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Hills", 1, 50f);
            // The one chunk stands at 60 m: the ring around it must meet it exactly and every seam must match.
            var chunk = session.World.Records.OfType<TerrainChunkRecord>().Single();
            var n = chunk.Heights.Resolution;
            session.Put(chunk.WithHeights(HeightField.Flat(60f, n)));

            SpaceEdits.ExtendLand(session, id, LandSides.All, 1);

            HeightField At(int x, int z) => session.World.Records.OfType<TerrainChunkRecord>().Single(c => c.Space == id && c.Coord == new ChunkCoord(x, z)).Heights.Load();
            var last = n - 1;
            // North-east corner (1, 1): its west column is the north strip's east column, its south row the east strip's north row.
            for (var i = 0; i < n; i++)
            {
                Assert.That(At(1, 1)[0, i], Is.EqualTo(At(0, 1)[last, i]).Within(1e-4f), $"west seam at {i}");
                Assert.That(At(1, 1)[i, 0], Is.EqualTo(At(1, 0)[i, last]).Within(1e-4f), $"south seam at {i}");
            }
            Assert.That(At(1, 1)[0, 0], Is.EqualTo(60f).Within(1e-4f), "The corner touches the old land's corner.");
            Assert.That(At(1, 1)[last, last], Is.EqualTo(50f).Within(1e-4f), "Its far corner is at the base height.");
        }

        [Test]
        public void ExtendLand_OnEverySide_CountsBothSidesAgainstTheReach()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Wide", 2, 50f);
            var half = (SpaceEdits.MaxExtent - 2) / 2;
            var step = Math.Min(half, SpaceEdits.MaxExtendBy);
            for (var grown = 0; grown + step <= half; grown += step)
                SpaceEdits.ExtendLand(session, id, LandSides.All, step);
            var width = LandBounds.Of(session.World.Records, id).Value.Width;

            Assert.Throws<ArgumentOutOfRangeException>(() => SpaceEdits.ExtendLand(session, id, LandSides.All, (SpaceEdits.MaxExtent - width) / 2 + 1));
            Assert.Throws<ArgumentException>(() => SpaceEdits.ExtendLand(session, id, Array.Empty<LandSide>(), 1));
        }

        [Test]
        public void ExtendLand_StopsAtTheLargestSupportedReach()
        {
            var session = NewSession();
            var id = SpaceEdits.CreateWorldspace(session, "Vast", 1, 50f);
            for (var reach = 1; reach + SpaceEdits.MaxExtendBy <= SpaceEdits.MaxExtent; reach += SpaceEdits.MaxExtendBy)
                SpaceEdits.ExtendLand(session, id, LandSide.East, SpaceEdits.MaxExtendBy);
            var width = LandBounds.Of(session.World.Records, id).Value.Width;

            Assert.Throws<ArgumentOutOfRangeException>(() => SpaceEdits.ExtendLand(session, id, LandSide.East, SpaceEdits.MaxExtent - width + 1));
            Assert.DoesNotThrow(() => SpaceEdits.ExtendLand(session, id, LandSide.East, SpaceEdits.MaxExtent - width));
            Assert.That(LandBounds.Of(session.World.Records, id).Value.Width, Is.EqualTo(SpaceEdits.MaxExtent));
        }

        [Test]
        public void SizesOutOfRange_AreRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpaceEdits.CreateWorldspace(NewSession(), "Huge", SpaceEdits.MaxSize + 1, 50f));
            Assert.Throws<ArgumentException>(() => SpaceEdits.CreateInterior(NewSession(), " "));
        }
    }
}
