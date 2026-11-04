using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Placement;
using Forge.Editing.Transforms;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>The move tool's slide over the fixture's slope (10 m at x = 0, +1 m per 10 m east).</summary>
    public class GroundSlideTests
    {
        static readonly Vector3 Down = -Vector3.UnitY;

        // A ray straight down through (x, z).
        static Vector3 Above(float x, float z) => new Vector3(x, 100f, z);

        static PlacedObjectRecord PlaceOne(BuildingFixture world, RecordId kind, float x, float z, float heading = 0f)
        {
            var placed = new PlaceObjectsCommand(world.Context).Execute(new PlaceObjectsCommand.Arguments
            {
                Objects = new[] { new PlaceObjectsCommand.Item { Object = kind.ToString(), X = x, Z = z, Heading = heading, Scale = 1f } }.ToList(),
            }).Placed[0];
            return world.Session.World.Records.Get<PlacedObjectRecord>(RecordId.Parse(placed.Id));
        }

        // Grabs the object at its pivot, from straight above.
        static GroundSlide Grab(BuildingFixture world, PlacedObjectRecord placed) =>
            GroundSlide.TryBegin(placed.Position, placed.Position, Above(placed.Position.X, placed.Position.Z), Down,
                world.Context.Seating, id => world.Session.World.Records.TryGet(id, out var r) ? r as BaseObjectRecord : null);

        [Test]
        public void Slide_SetsTheObjectDownOnTheGroundAtItsNewSpot_KeepingHeadingAndSize()
        {
            var world = BuildingFixture.Create();
            var house = PlaceOne(world, BuildingFixture.House, 30f, 30f, heading: 90f);
            var slide = Grab(world, house);

            slide.Update(Above(50f, 35f), Down, snap: false);
            var moved = slide.Apply(house);

            Assert.That(Vector3.Distance(moved.Position, new Vector3(50f, 15f, 35f)), Is.LessThan(1e-3f), "Uphill: 1 m per 10 m east.");
            Assert.That(PlacementMath.HeadingOf(moved.Rotation), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(moved.Scale, Is.EqualTo(house.Scale));
            Assert.That(Vector3.Distance(slide.HandlePosition, house.Position + new Vector3(20f, 0f, 5f)), Is.LessThan(1e-3f), "The gizmo follows level.");
        }

        [Test]
        public void ASlidFence_SitsExactlyLikeOnePlacedThere()
        {
            var world = BuildingFixture.Create();
            var fence = PlaceOne(world, BuildingFixture.Fence, 30f, 30f, heading: 30f);
            var reference = PlaceOne(world, BuildingFixture.Fence, 70f, 50f, heading: 30f);
            var slide = Grab(world, fence);

            slide.Update(Above(70f, 50f), Down, snap: false);
            var moved = slide.Apply(fence);

            Assert.That(Vector3.Distance(moved.Position, reference.Position), Is.LessThan(1e-3f));
            Assert.That(System.Math.Abs(Quaternion.Dot(moved.Rotation, reference.Rotation)), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void WithSnap_TheSlideGoesInHalfMeters()
        {
            var world = BuildingFixture.Create();
            var house = PlaceOne(world, BuildingFixture.House, 30f, 30f);
            var slide = Grab(world, house);

            slide.Update(Above(32.2f, 34.9f), Down, snap: true);

            Assert.That(Vector3.Distance(slide.Delta, new Vector3(2f, 0f, 5f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void WhereThereIsNoGround_TheObjectMovesLevel()
        {
            var world = BuildingFixture.Create();
            var house = PlaceOne(world, BuildingFixture.House, 30f, 30f);
            var slide = Grab(world, house);

            slide.Update(Above(500f, 30f), Down, snap: false);
            var moved = slide.Apply(house);

            Assert.That(Vector3.Distance(moved.Position, house.Position + new Vector3(470f, 0f, 0f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void BeforeThePointerMoves_NothingChanges()
        {
            var world = BuildingFixture.Create();
            var house = PlaceOne(world, BuildingFixture.House, 30f, 30f);
            var slide = Grab(world, house);

            Assert.That(slide.IsIdentity, Is.True);
            Assert.That(slide.Apply(house), Is.SameAs(house));
        }
    }
}
