using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>Moving, turning and resizing placed objects on the fixture's slope (10 m at x = 0, +1 m per 10 m east).</summary>
    public class AdjustObjectsTests
    {
        static PlacedObjectRecord PlaceOne(BuildingFixture world, RecordId kind, float x, float z, float heading = 0f)
        {
            var placed = new PlaceObjectsCommand(world.Context).Execute(new PlaceObjectsCommand.Arguments
            {
                Objects = new[] { new PlaceObjectsCommand.Item { Object = kind.ToString(), X = x, Z = z, Heading = heading, Scale = 1f } }.ToList(),
            }).Placed[0];
            // The world is read after placing: each edit publishes a new one.
            return world.Session.World.Records.Get<PlacedObjectRecord>(RecordId.Parse(placed.Id));
        }

        static AdjustObjectsCommand.Result Adjust(BuildingFixture world, params AdjustObjectsCommand.Item[] items) =>
            new AdjustObjectsCommand(world.Context).Execute(new AdjustObjectsCommand.Arguments { Objects = items.ToList() });

        static AdjustObjectsCommand.Item Item(PlacedObjectRecord placed, float? x = null, float? z = null, float? heading = null, float? scale = null) =>
            new AdjustObjectsCommand.Item { Id = placed.Id.ToString(), X = x, Z = z, Heading = heading, Scale = scale };

        static PlacedObjectRecord Current(BuildingFixture world, PlacedObjectRecord placed) =>
            world.Session.World.Records.Get<PlacedObjectRecord>(placed.Id);

        [Test]
        public void Move_SetsTheObjectDownOnTheGroundAtItsNewSpot_AsOneUndoStep()
        {
            var world = BuildingFixture.Create();
            var house = PlaceOne(world, BuildingFixture.House, 30f, 30f, heading: 180f);

            var result = Adjust(world, Item(house, x: 60f));

            var moved = Current(world, house);
            Assert.That(Vector3.Distance(moved.Position, new Vector3(60f, 16f, 30f)), Is.LessThan(1e-3f));
            Assert.That(PlacementMath.HeadingOf(moved.Rotation), Is.EqualTo(180f).Within(1e-3f), "What isn't given stays as it was.");
            Assert.That(result.Summary, Does.StartWith("Adjusted 1 object(s): 1 Small House."));
            Assert.That(world.Session.UndoDescription, Does.StartWith("Adjust"));

            world.Session.Undo();
            Assert.That(Current(world, house).Position.X, Is.EqualTo(30f));
        }

        [Test]
        public void TurnAndResize_KeepTheObjectWhereItStands()
        {
            var world = BuildingFixture.Create();
            var house = PlaceOne(world, BuildingFixture.House, 30f, 30f);

            Adjust(world, Item(house, heading: 90f, scale: 2f));

            var changed = Current(world, house);
            Assert.That((changed.Position.X, changed.Position.Z), Is.EqualTo((30f, 30f)));
            Assert.That(PlacementMath.HeadingOf(changed.Rotation), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(changed.Scale, Is.EqualTo(2f));
        }

        [Test]
        public void AMovedObject_SitsExactlyLikeOnePlacedThere()
        {
            var world = BuildingFixture.Create();
            var fence = PlaceOne(world, BuildingFixture.Fence, 30f, 30f, heading: 0f);
            var reference = PlaceOne(world, BuildingFixture.Fence, 70f, 50f, heading: 30f);

            Adjust(world, Item(fence, x: 70f, z: 50f, heading: 30f));

            var moved = Current(world, fence);
            Assert.That(Vector3.Distance(moved.Position, reference.Position), Is.LessThan(1e-4f));
            Assert.That(System.Math.Abs(Quaternion.Dot(moved.Rotation, reference.Rotation)), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void UnknownIdsAndSpotsWithoutTerrain_AreSkipped_TheRestApplies()
        {
            var world = BuildingFixture.Create();
            var house = PlaceOne(world, BuildingFixture.House, 30f, 30f);
            var tree = PlaceOne(world, BuildingFixture.Tree, 40f, 40f);

            var result = Adjust(world,
                Item(house, x: 50f),
                Item(tree, x: 500f),
                new AdjustObjectsCommand.Item { Id = "user:ref/nope", X = 10f });

            Assert.That(result.Adjusted.Select(o => o.Id), Is.EqualTo(new[] { house.Id.ToString() }));
            Assert.That(result.Skipped.Select(s => s.Id), Is.EqualTo(new[] { tree.Id.ToString(), "user:ref/nope" }));
            Assert.That(result.Skipped[0].Reason, Does.Contain("no terrain"));
            Assert.That(Current(world, tree).Position.X, Is.EqualTo(40f));
        }

        [Test]
        public void NothingToChange_IsRefused()
        {
            var world = BuildingFixture.Create();

            Assert.Throws<CommandException>(() => Adjust(world, new AdjustObjectsCommand.Item { Id = "user:ref/nope", X = 10f }));
            Assert.That(world.Session.CanUndo, Is.False);
        }

        [Test]
        public void AnObjectListedTwice_IsRefused()
        {
            var world = BuildingFixture.Create();
            var house = PlaceOne(world, BuildingFixture.House, 30f, 30f);

            var error = Assert.Throws<CommandException>(() => Adjust(world, Item(house, x: 40f), Item(house, heading: 90f)));

            Assert.That(error.Message, Does.Contain("more than once"));
            Assert.That(Current(world, house).Position.X, Is.EqualTo(30f));
        }
    }
}
