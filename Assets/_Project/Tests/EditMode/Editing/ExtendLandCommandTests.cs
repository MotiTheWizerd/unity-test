using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Spaces;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>Growing the land through the agent's extend_land command.</summary>
    public class ExtendLandCommandTests
    {
        // The fixture's land is 2×2 chunks (x, z in 0..1) of Space.
        static BuildingFixture World() =>
            BuildingFixture.Create(new SpaceRecord(BuildingFixture.Space, "Main", SpaceKind.Worldspace, 10f));

        static ExtendLandCommand.Result Extend(BuildingFixture world, string side, int chunks) =>
            new ExtendLandCommand(world.Context).Execute(new ExtendLandCommand.Arguments { Side = side, Chunks = chunks });

        [Test]
        public void ExtendLand_GrowsTheLand_AndReportsItsReach()
        {
            var world = World();

            var result = Extend(world, "West", 3);

            Assert.That(result.ChunksAdded, Is.EqualTo(6));
            Assert.That(result.LandReach, Is.EqualTo("5×2 chunks (320 × 128 m)"));
            Assert.That(result.Summary, Is.EqualTo("Extended Main 192 m west (6 chunks). The land is now 5×2 chunks (320 × 128 m)."));
            Assert.That(LandBounds.Of(world.Session.World.Records, BuildingFixture.Space).Value.MinX, Is.EqualTo(-3));
            Assert.That(world.Context.Terrain.HeightAt(new System.Numerics.Vector2(-100f, 50f)), Is.Not.Null, "The commands see the new land at once.");
        }

        [Test]
        public void ExtendLand_RefusesInteriors_UnknownSides_AndBadCounts()
        {
            var interior = BuildingFixture.Create(new SpaceRecord(BuildingFixture.Space, "Cellar", SpaceKind.Interior));
            Assert.That(() => Extend(interior, "north", 1), Throws.TypeOf<CommandException>().With.Message.Contains("interior"));

            var world = World();
            Assert.That(() => Extend(world, "up", 1), Throws.TypeOf<CommandException>().With.Message.Contains("north, south, east or west"));
            Assert.That(() => Extend(world, "north", 0), Throws.TypeOf<CommandException>());
        }
    }
}
