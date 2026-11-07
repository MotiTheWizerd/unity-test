using System;
using System.Linq;
using System.Numerics;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>Hills, ridges and smoothing on the fixture's slope: 10 m at x = 0, rising 1 m per 10 m eastwards.</summary>
    public class SculptingTests
    {
        static readonly Vector2 Middle = new Vector2(64f, 64f);

        static LandformSculpting Landform(BuildingFixture world, Vector2[] spine, float width, float height,
            LandformProfile profile = LandformProfile.Round, float roughness = 0f) =>
            new LandformSculpting(world.Session.World.Records, BuildingFixture.Space, spine, width, height, profile, roughness);

        static float GroundAt(BuildingFixture world, float x, float z) =>
            new TerrainGrid(world.Session.World.Records, BuildingFixture.Space).HeightAt(new Vector2(x, z)).Value;

        static GroundPoint Point(float x, float z) => new GroundPoint { X = x, Z = z };

        [Test]
        public void AHill_RisesByItsHeightAtTheCenter_AndMeetsTheLandAtItsFoot()
        {
            var hill = Landform(BuildingFixture.Create(), new[] { Middle }, width: 40f, height: 20f);

            Assert.That(hill.HeightChangeAt(Middle), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(hill.HeightChangeAt(new Vector2(74f, 64f)), Is.EqualTo(10f).Within(1e-4f), "Halfway down a round hill, half its height.");
            Assert.That(hill.HeightChangeAt(new Vector2(64f, 84f)), Is.Zero, "The foot.");
            Assert.That(hill.Highest, Is.EqualTo(36.4f).Within(1e-4f), "16.4 m of ground, 20 m of hill.");
            Assert.That(hill.Description, Is.EqualTo("Sculpt hill"));
        }

        [TestCase(LandformProfile.Round, 0.5f, 0.5f)]
        [TestCase(LandformProfile.Peak, 0.5f, 0.25f)]
        [TestCase(LandformProfile.Plateau, 0.4f, 1f)]
        [TestCase(LandformProfile.Plateau, 0.75f, 0.5f)]
        public void Profiles_ShapeTheSide(LandformProfile profile, float t, float share)
        {
            Assert.That(LandformSculpting.Fall(profile, 0f), Is.EqualTo(1f));
            Assert.That(LandformSculpting.Fall(profile, t), Is.EqualTo(share).Within(1e-5f));
            Assert.That(LandformSculpting.Fall(profile, 1f), Is.Zero);
        }

        [Test]
        public void ANegativeHeight_DigsAHollow()
        {
            var hollow = Landform(BuildingFixture.Create(), new[] { Middle }, width: 20f, height: -5f);

            Assert.That(hollow.HeightChangeAt(Middle), Is.EqualTo(-5f).Within(1e-4f));
            Assert.That(hollow.MostLowered, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(hollow.MostRaised, Is.Zero);
            Assert.That(hollow.Description, Is.EqualTo("Sculpt hollow"));
        }

        [Test]
        public void ARidge_RunsAlongItsPath_WithRoundedEnds()
        {
            var ridge = Landform(BuildingFixture.Create(), new[] { new Vector2(30f, 64f), new Vector2(90f, 64f) }, width: 20f, height: 10f);

            Assert.That(ridge.HeightChangeAt(new Vector2(40f, 64f)), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(ridge.HeightChangeAt(new Vector2(80f, 64f)), Is.EqualTo(10f).Within(1e-4f), "The crest is level along the path.");
            Assert.That(ridge.HeightChangeAt(new Vector2(60f, 69f)), Is.EqualTo(5f).Within(1e-4f), "Halfway to the foot.");
            Assert.That(ridge.HeightChangeAt(new Vector2(95f, 64f)), Is.EqualTo(5f).Within(1e-4f), "5 m past the end, the same.");
            Assert.That(ridge.HeightChangeAt(new Vector2(60f, 75f)), Is.Zero);
            Assert.That(ridge.Description, Is.EqualTo("Sculpt ridge"));
        }

        [Test]
        public void Roughness_BreaksTheShapeUp_ButKeepsItsFootAndItsSide()
        {
            var world = BuildingFixture.Create();
            var smooth = Landform(world, new[] { Middle }, 100f, 30f, LandformProfile.Peak);
            var rough = Landform(world, new[] { Middle }, 100f, 30f, LandformProfile.Peak, roughness: 1f);
            var again = Landform(world, new[] { Middle }, 100f, 30f, LandformProfile.Peak, roughness: 1f);

            var ring = Enumerable.Range(0, 36).Select(i => Middle + 25f * new Vector2((float)Math.Cos(i * Math.PI / 18), (float)Math.Sin(i * Math.PI / 18))).ToList();
            var differences = ring.Select(p => Math.Abs(rough.HeightChangeAt(p).Value - smooth.HeightChangeAt(p).Value)).ToList();
            Assert.That(differences.Max(), Is.GreaterThan(1f), "Halfway down, the rough mountain is not round.");
            Assert.That(ring.Select(p => again.HeightChangeAt(p).Value), Is.EqualTo(ring.Select(p => rough.HeightChangeAt(p).Value)), "The same each time.");
            Assert.That(rough.MostLowered, Is.Zero, "Raising never digs.");
            Assert.That(rough.HeightChangeAt(new Vector2(64f, 114.5f)), Is.Zero, "Past the foot, nothing.");
            Assert.That(rough.HeightChangeAt(new Vector2(64f, 113f)), Is.LessThan(0.1f), "At the foot it meets the land.");
        }

        [Test]
        public void TheTerrainsTop_StopsAMountain_AndSaysSo()
        {
            var tall = Landform(BuildingFixture.Create(), new[] { Middle }, 60f, WorldGrid.MaxTerrainHeight);

            Assert.That(tall.Highest, Is.EqualTo(WorldGrid.MaxTerrainHeight));
            Assert.That(tall.ReachedTheLimit, Is.True);
        }

        [Test]
        public void Landforms_RefuseWhatTheyCantBe()
        {
            var store = BuildingFixture.Create().Session.World.Records;
            LandformSculpting Make(Vector2[] spine, float width, float height) =>
                new LandformSculpting(store, BuildingFixture.Space, spine, width, height, LandformProfile.Round, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => Make(new[] { Middle }, 20f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(new[] { Middle }, LandformSculpting.MaxWidth + 1f, 10f));
            Assert.Throws<ArgumentException>(() => Make(new[] { Vector2.Zero, new Vector2(600f, 0f) }, 20f, 10f), "Too long to sculpt at once.");
        }

        [Test]
        public void SculptHill_RaisesIt_AsOneUndoStep_AndSaysHowHigh()
        {
            var world = BuildingFixture.Create();

            var result = new SculptHillCommand(world.Context).Execute(new SculptHillCommand.Arguments
            {
                X = 64f, Z = 64f, Width = 40f, Height = 20f, Roughness = 0f,
            });

            Assert.That(result.Summary, Is.EqualTo("Raised a round hill 20 m high and 40 m across around (64, 64): the ground now reaches 36.4 m at its highest."));
            Assert.That(result.Highest, Is.EqualTo(36.4f));
            Assert.That(GroundAt(world, 64f, 64f), Is.EqualTo(36.4f).Within(1e-4f));
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Sculpt hill"));

            world.Session.Undo();
            Assert.That(GroundAt(world, 64f, 64f), Is.EqualTo(16.4f).Within(1e-4f));
        }

        [Test]
        public void SculptHill_DigsWithANegativeHeight_AndTakesAProfileByName()
        {
            var world = BuildingFixture.Create();

            var result = new SculptHillCommand(world.Context).Execute(new SculptHillCommand.Arguments
            {
                X = 64f, Z = 64f, Width = 20f, Height = -6f, Profile = "Peak", Roughness = 0f,
            });

            Assert.That(result.Summary, Is.EqualTo("Dug a peaked hollow 6 m deep and 20 m across around (64, 64): the ground now goes down to 10.4 m at its lowest."));
            Assert.That(GroundAt(world, 69f, 64f), Is.EqualTo(16.9f - 6f * 0.25f).Within(1e-4f), "Halfway out, a quarter as deep.");
        }

        [Test]
        public void SculptHill_IsRefused_WithWhatWouldWork()
        {
            var hill = new SculptHillCommand(BuildingFixture.Create().Context);
            SculptHillCommand.Arguments At(float x, float height, string profile = null) =>
                new SculptHillCommand.Arguments { X = x, Z = 64f, Width = 20f, Height = height, Profile = profile };

            Assert.That(() => hill.Execute(At(64f, 10f, "volcano")),
                Throws.TypeOf<CommandException>().With.Message.EqualTo("There is no profile 'volcano'. The profiles are: round, peak, plateau."));
            Assert.That(() => hill.Execute(At(64f, 0f)), Throws.TypeOf<CommandException>().With.Message.Contains("changes nothing"));
            Assert.That(() => hill.Execute(At(500f, 10f)), Throws.TypeOf<CommandException>().With.Message.Contains("no land under that landform"));
        }

        [Test]
        public void Sculpting_CarriesTheObjectsOnTheGround()
        {
            var world = BuildingFixture.Create();
            new PlaceObjectsCommand(world.Context).Execute(new PlaceObjectsCommand.Arguments
            {
                Objects = new[] { new PlaceObjectsCommand.Item { Object = BuildingFixture.House.ToString(), X = 64f, Z = 64f, Heading = 0f } }.ToList(),
            });
            var house = world.Session.World.Records.OfType<PlacedObjectRecord>().Single();

            var result = new SculptHillCommand(world.Context).Execute(new SculptHillCommand.Arguments { X = 64f, Z = 64f, Width = 60f, Height = 12f, Roughness = 0f });

            Assert.That(result.MovedObjects, Is.EqualTo(1));
            Assert.That(result.Summary, Does.EndWith("1 object(s) moved with the ground."));
            Assert.That(world.Session.World.Records.Get<PlacedObjectRecord>(house.Id).Position.Y, Is.EqualTo(house.Position.Y + 12f).Within(1e-3f));
        }

        [Test]
        public void SculptRidge_Closed_MakesARing_AndLeavesItsMiddle()
        {
            var world = BuildingFixture.Create();

            var result = new SculptRidgeCommand(world.Context).Execute(new SculptRidgeCommand.Arguments
            {
                Points = new[] { Point(40f, 40f), Point(88f, 40f), Point(88f, 88f), Point(40f, 88f) }.ToList(),
                Closed = true, Width = 10f, Height = 8f, Roughness = 0f,
            });

            Assert.That(result.Summary, Does.StartWith("Raised a round ridge 8 m high and 10 m wide along 192 m"));
            Assert.That(GroundAt(world, 40f, 64f), Is.EqualTo(14f + 8f).Within(1e-4f), "The way back to the first point is part of it.");
            Assert.That(GroundAt(world, 64f, 64f), Is.EqualTo(16.4f).Within(1e-4f), "The middle of the ring is untouched.");
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Sculpt ridge"));
        }

        [Test]
        public void SculptRidge_WithANegativeHeight_DigsAValley()
        {
            var world = BuildingFixture.Create();

            var result = new SculptRidgeCommand(world.Context).Execute(new SculptRidgeCommand.Arguments
            {
                Points = new[] { Point(20f, 64f), Point(100f, 64f) }.ToList(), Width = 30f, Height = -4f, Roughness = 0f,
            });

            Assert.That(result.Summary, Does.StartWith("Dug a round valley 4 m deep and 30 m wide along 80 m"));
            Assert.That(GroundAt(world, 50f, 64f), Is.EqualTo(15f - 4f).Within(1e-4f));
        }

        [Test]
        public void Smoothing_FlattensABump_AndLeavesAnEvenSlopeAsItIs()
        {
            var world = BuildingFixture.Create();
            new SculptHillCommand(world.Context).Execute(new SculptHillCommand.Arguments { X = 64f, Z = 64f, Width = 6f, Height = 4f, Profile = "peak", Roughness = 0f });

            var result = new SmoothGroundCommand(world.Context).Execute(new SmoothGroundCommand.Arguments { X = 64f, Z = 64f, Width = 20f, Depth = 20f, Size = 8f });

            Assert.That(GroundAt(world, 64f, 64f) - 16.4f, Is.LessThan(1f), "The 4 m bump is mostly gone.");
            Assert.That(GroundAt(world, 70f, 70f), Is.EqualTo(17f).Within(0.05f), "The slope beside it stays.");
            Assert.That(GroundAt(world, 64f, 82f), Is.EqualTo(16.4f).Within(1e-4f), "Past the blend nothing moves.");
            Assert.That(result.MostLowered, Is.GreaterThan(2f));
            Assert.That(result.Summary, Does.StartWith("Smoothed a 20 × 20 m area around (64, 64), heading 0° over 8 m stretches: raised"));
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Smooth ground"));
        }

        [Test]
        public void Smoothing_TakesAnyShape_AndRefusesNoLand()
        {
            var world = BuildingFixture.Create();
            var smooth = new SmoothGroundCommand(world.Context);

            var triangle = smooth.Execute(new SmoothGroundCommand.Arguments { Points = new[] { Point(40f, 40f), Point(80f, 40f), Point(60f, 80f) }.ToList() });

            Assert.That(triangle.Summary, Does.StartWith("Smoothed a 3-cornered area of about 800 m²"));
            Assert.That(() => smooth.Execute(new SmoothGroundCommand.Arguments { X = 500f, Z = 500f, Width = 10f, Depth = 10f }),
                Throws.TypeOf<CommandException>().With.Message.Contains("no land under that area"));
        }
    }
}
