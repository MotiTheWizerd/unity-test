using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Spaces;
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
        public void SizesOutOfRange_AreRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpaceEdits.CreateWorldspace(NewSession(), "Huge", SpaceEdits.MaxSize + 1, 50f));
            Assert.Throws<ArgumentException>(() => SpaceEdits.CreateInterior(NewSession(), " "));
        }
    }
}
