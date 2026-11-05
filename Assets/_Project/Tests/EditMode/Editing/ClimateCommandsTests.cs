using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Tests.Data;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>The climate through the agent's command: set_climate.</summary>
    public class ClimateCommandsTests
    {
        static BuildingFixture World(SpaceKind kind = SpaceKind.Worldspace) =>
            BuildingFixture.Create(ClimateTests.Northern(), new SpaceRecord(BuildingFixture.Space, "Main", kind, 10f));

        static SetClimateCommand.Result SetClimate(BuildingFixture world, string climate) =>
            new SetClimateCommand(world.Context).Execute(new SetClimateCommand.Arguments { Climate = climate });

        static SpaceRecord Space(BuildingFixture world) => world.Session.World.Records.Get<SpaceRecord>(BuildingFixture.Space);

        [TestCase("Northern")]
        [TestCase("northern ")]
        [TestCase("core:climate/northern")]
        public void SetClimate_ByNameOrId_GivesTheWorldspaceItsClimate(string climate)
        {
            var world = World();

            var result = SetClimate(world, climate);

            Assert.That(Space(world).Climate?.ToString(), Is.EqualTo("core:climate/northern"));
            Assert.That(result.Climate, Is.EqualTo("core:climate/northern"));
            Assert.That(result.Summary, Is.EqualTo("Main now has the Northern climate: a 120-day year, 18 h of daylight at midsummer and 8 h at midwinter."));
        }

        [Test]
        public void SetClimate_WithoutAClimate_RemovesIt()
        {
            var world = World();
            SetClimate(world, "Northern");

            var result = SetClimate(world, null);

            Assert.That(Space(world).Climate, Is.Null);
            Assert.That(result.Climate, Is.Null);
            Assert.That(result.Summary, Does.StartWith("Removed the climate of Main"));
        }

        [Test]
        public void SetClimate_ToAnUnknownClimate_IsRefused_ListingTheClimates()
        {
            Assert.That(() => SetClimate(World(), "Tropical"),
                Throws.TypeOf<CommandException>().With.Message.Contains("Northern (core:climate/northern)"));
        }

        [Test]
        public void SetClimate_InAnInterior_IsRefused()
        {
            Assert.That(() => SetClimate(World(SpaceKind.Interior), "Northern"),
                Throws.TypeOf<CommandException>().With.Message.Contains("interior"));
        }
    }
}
