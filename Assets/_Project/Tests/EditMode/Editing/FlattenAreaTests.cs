using System;
using System.Linq;
using System.Numerics;
using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Geometry;
using Forge.Editing.Terrain;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>Levelling ground on the fixture's slope: 10 m at x = 0, rising 1 m per 10 m eastwards.</summary>
    public class FlattenAreaTests
    {
        static FlattenAreaCommand.Result Flatten(BuildingFixture world, float x, float z, float width, float depth,
            float? heading = null, float? height = null, float? blend = null) =>
            new FlattenAreaCommand(world.Context).Execute(new FlattenAreaCommand.Arguments
            {
                X = x, Z = z, Width = width, Depth = depth, Heading = heading, Height = height, Blend = blend,
            });

        static float GroundAt(BuildingFixture world, float x, float z) =>
            new TerrainGrid(world.Session.World.Records, BuildingFixture.Space).HeightAt(new Vector2(x, z)).Value;

        [Test]
        public void Flattening_LevelsTheArea_AndEasesBackOverTheBlend()
        {
            var world = BuildingFixture.Create();
            var flattening = new AreaFlattening(world.Session.World.Records, BuildingFixture.Space, new GroundRect(new Vector2(50f, 50f), 20f, 10f), 15f, blend: 4f);

            Assert.That(flattening.SampleHeight(new Vector2(41f, 46f)), Is.EqualTo(15f).Within(1e-4f));
            Assert.That(flattening.SampleHeight(new Vector2(59f, 54f)), Is.EqualTo(15f).Within(1e-4f));
            // Halfway through the blend the ground is halfway back: 16.2 natural, 15 level.
            Assert.That(flattening.SampleHeight(new Vector2(62f, 50f)), Is.EqualTo(15.6f).Within(1e-4f));
            Assert.That(flattening.SampleHeight(new Vector2(64f, 50f)), Is.EqualTo(16.4f).Within(1e-4f), "The blend's far edge is untouched.");
            Assert.That(flattening.SampleHeight(new Vector2(50f, 60f)), Is.EqualTo(15f).Within(1e-4f), "North of the area, ground at 15 m stays.");
            Assert.That(flattening.MostRaised, Is.EqualTo(1f).Within(1e-4f), "The west edge, 14 m, came up to 15.");
            Assert.That(flattening.MostLowered, Is.EqualTo(1f).Within(1e-4f), "The east edge, 16 m, came down to 15.");
        }

        [Test]
        public void Flattening_WithoutBlend_StopsAtTheEdge()
        {
            var world = BuildingFixture.Create();
            var flattening = new AreaFlattening(world.Session.World.Records, BuildingFixture.Space, new GroundRect(new Vector2(50f, 50f), 20f, 10f), 15f, blend: 0f);

            Assert.That(flattening.SampleHeight(new Vector2(60f, 50f)), Is.EqualTo(15f).Within(1e-4f));
            Assert.That(flattening.SampleHeight(new Vector2(61f, 50f)), Is.EqualTo(16.1f).Within(1e-4f));
        }

        [Test]
        public void Flattening_RejectsOutOfRangeValues()
        {
            var store = BuildingFixture.Create().Session.World.Records;
            var area = new GroundRect(new Vector2(50f, 50f), 10f, 10f);

            Assert.Throws<ArgumentOutOfRangeException>(() => new AreaFlattening(store, BuildingFixture.Space, area, -1f, 4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AreaFlattening(store, BuildingFixture.Space, area, 15f, AreaFlattening.MaxBlend + 1f));
        }

        [Test]
        public void Command_LevelsToTheAverageHeight_AsOneUndoStep()
        {
            var world = BuildingFixture.Create();

            var result = Flatten(world, 50f, 50f, 20f, 10f);

            Assert.That(result.Height, Is.EqualTo(15f));
            Assert.That(GroundAt(world, 42f, 50f), Is.EqualTo(15f).Within(1e-4f));
            Assert.That(GroundAt(world, 58f, 50f), Is.EqualTo(15f).Within(1e-4f));
            Assert.That(world.Session.UndoDescription, Is.EqualTo("Flatten area"));
            Assert.That(result.Summary, Does.Contain("Levelled a 20 × 10 m area"));

            world.Session.Undo();
            Assert.That(GroundAt(world, 58f, 50f), Is.EqualTo(15.8f).Within(1e-4f));
        }

        [Test]
        public void Command_Heading_TurnsTheArea()
        {
            var world = BuildingFixture.Create();

            // Facing east: 20 m north-south, 10 m east-west.
            Flatten(world, 50f, 50f, 20f, 10f, heading: 90f, height: 15f, blend: 0f);

            Assert.That(GroundAt(world, 50f, 59f), Is.EqualTo(15f).Within(1e-4f));
            Assert.That(GroundAt(world, 58f, 50f), Is.EqualTo(15.8f).Within(1e-4f), "8 m east is outside a 10 m deep area.");
        }

        [Test]
        public void Command_ObjectsOnTheSite_SettleOnTheNewGround()
        {
            var world = BuildingFixture.Create();
            new PlaceObjectsCommand(world.Context).Execute(new PlaceObjectsCommand.Arguments
            {
                Objects = new[] { new PlaceObjectsCommand.Item { Object = BuildingFixture.Fence.ToString(), X = 55f, Z = 50f, Heading = 0f } }.ToList(),
            });
            var fence = world.Session.World.Records.OfType<PlacedObjectRecord>().Single();
            Assert.That(Vector3.Transform(Vector3.UnitY, fence.Rotation).Y, Is.LessThan(0.999f), "Placed on the slope, the fence leans with it.");

            var result = Flatten(world, 50f, 50f, 20f, 10f, height: 15f);

            var settled = world.Session.World.Records.Get<PlacedObjectRecord>(fence.Id);
            Assert.That(result.MovedObjects, Is.EqualTo(1));
            Assert.That(settled.Position.Y, Is.EqualTo(15f).Within(1e-3f));
            Assert.That(Vector3.Transform(Vector3.UnitY, settled.Rotation).Y, Is.EqualTo(1f).Within(1e-5f), "On level ground it stands upright.");
        }

        [Test]
        public void Command_ReportsGroundThatIsAlreadyLevel()
        {
            var world = BuildingFixture.Create();
            Flatten(world, 50f, 50f, 20f, 10f, height: 15f, blend: 0f);

            var again = Flatten(world, 50f, 50f, 20f, 10f, height: 15f, blend: 0f);

            Assert.That(again.Summary, Does.Contain("already level"));
            world.Session.Undo();
            Assert.That(world.Session.CanUndo, Is.False, "The second, empty flatten added no undo step.");
        }

        [Test]
        public void Command_OffTheTerrain_IsRefused()
        {
            var world = BuildingFixture.Create();

            var error = Assert.Throws<CommandException>(() => Flatten(world, 500f, 500f, 10f, 10f));

            Assert.That(error.Message, Does.Contain("no terrain"));
            Assert.That(world.Session.CanUndo, Is.False);
        }
    }
}
