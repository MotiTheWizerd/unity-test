using System.Linq;
using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>The sea through the agent's commands: set_sea_level, and the water inspect_area reports.</summary>
    public class SeaCommandsTests
    {
        // The fixture's land rises from 10 m at x = 0 by 1 m per 10 m eastwards, so a sea at 15 m covers x < 50.
        static BuildingFixture World(float? seaLevel = null) =>
            BuildingFixture.Create(new SpaceRecord(BuildingFixture.Space, "Main", SpaceKind.Worldspace, 10f, seaLevel));

        static SetSeaLevelCommand.Result SetSea(BuildingFixture world, float? level) =>
            new SetSeaLevelCommand(world.Context).Execute(new SetSeaLevelCommand.Arguments { Level = level });

        static InspectAreaCommand.Result Inspect(BuildingFixture world, float x, float z, float size) =>
            new InspectAreaCommand(world.Context).Execute(new InspectAreaCommand.Arguments { X = x, Z = z, Size = size });

        [Test]
        public void SetSeaLevel_FloodsTheLowGround_AndCountsWhatStandsUnderWater()
        {
            var world = World();
            new PlaceObjectsCommand(world.Context).Execute(new PlaceObjectsCommand.Arguments
            {
                Objects = new[] { 20f, 80f }
                    .Select(x => new PlaceObjectsCommand.Item { Object = BuildingFixture.House.ToString(), X = x, Z = 20f })
                    .ToList(),
            });

            var result = SetSea(world, 15f);

            Assert.That(world.Session.World.Records.Get<SpaceRecord>(BuildingFixture.Space).WaterLevel, Is.EqualTo(15f));
            Assert.That(result.SeaLevel, Is.EqualTo(15f));
            Assert.That(result.ObjectsUnderWater, Is.EqualTo(1), "The house at x = 20 stands on 12 m ground.");
            Assert.That(result.Summary, Does.Contain("is now at 15 m").And.Contain("1 object(s) stand below it"));
            Assert.That(world.Context.Water.DepthAt(new System.Numerics.Vector2(20f, 20f)), Is.EqualTo(3f).Within(1e-4f),
                "The commands' water map follows the change at once.");
        }

        [Test]
        public void SetSeaLevel_WithoutALevel_RemovesTheSea()
        {
            var world = World(15f);

            var result = SetSea(world, null);

            Assert.That(world.Session.World.Records.Get<SpaceRecord>(BuildingFixture.Space).WaterLevel, Is.Null);
            Assert.That(result.SeaLevel, Is.Null);
            Assert.That(result.Summary, Is.EqualTo("Removed the sea of Main."));
        }

        [Test]
        public void SetSeaLevel_InAnInterior_IsRefused()
        {
            var world = BuildingFixture.Create(new SpaceRecord(BuildingFixture.Space, "Cellar", SpaceKind.Interior));

            Assert.That(() => SetSea(world, 2f), Throws.TypeOf<CommandException>().With.Message.Contains("interior"));
        }

        [Test]
        public void InspectArea_ReportsHowMuchIsUnderWater_AndHowDeep()
        {
            var world = World(15f);

            // x from 40 to 60: the ground at x < 50 (10 of 21 sample columns) is below the sea.
            var result = Inspect(world, 50f, 50f, 20f);

            Assert.That(result.Water.SeaLevel, Is.EqualTo(15f));
            Assert.That(result.Water.UnderwaterPercent, Is.EqualTo(48));
            Assert.That(result.Water.Deepest, Is.EqualTo(1f));
            Assert.That(result.Summary, Does.Contain("48% under water up to 1 m deep (sea level 15 m)"));
        }

        [Test]
        public void InspectArea_AboveTheSea_SaysItIsDry()
        {
            var result = Inspect(World(15f), 100f, 50f, 20f);

            Assert.That(result.Water.UnderwaterPercent, Is.EqualTo(0));
            Assert.That(result.Water.Deepest, Is.EqualTo(0f));
            Assert.That(result.Summary, Does.Contain("all above the sea (sea level 15 m)"));
        }

        [Test]
        public void InspectArea_WithoutASea_ReportsNoWater()
        {
            var result = Inspect(World(), 30f, 50f, 20f);

            Assert.That(result.Water, Is.Null);
            Assert.That(result.Summary, Does.Not.Contain("sea"));
        }
    }
}
