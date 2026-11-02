using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class ObjectSeatingTests
    {
        static readonly BaseObjectRecord Fence = new BaseObjectRecord(RecordId.Parse("core:fence"), "Fence", "fence",
            placement: new PlacementRules(false, 1f, 1f, alignToSlope: true));

        static readonly BaseObjectRecord Crate = new BaseObjectRecord(RecordId.Parse("core:crate"), "Crate", "crate");

        static readonly PlacementShape FenceShape = new PlacementShape(new Footprint(3f, 0.2f), 0f);
        static readonly PlacementShape CrateShape = new PlacementShape(new Footprint(1f, 1f), 0.5f);

        // A hillside rising to the east.
        static readonly Func<Vector2, float?> Hill = p => 0.4f * p.X + 10f;

        static ObjectSeating NewSeating(Func<Vector2, float?> ground = null) =>
            new ObjectSeating(
                ground ?? Hill,
                id => id == Fence.Id ? FenceShape : (PlacementShape?)null,
                key => key == "fence" ? FenceShape : CrateShape);

        [Test]
        public void UprightObjects_RestTheirLowestPointOnTheGround_TurnedByTheirHeading()
        {
            var pose = NewSeating().Seat(Crate, new Vector3(2f, 5f, 3f), onTerrain: true, yawDegrees: 90f, scale: 2f);

            Assert.That(pose.Position, Is.EqualTo(new Vector3(2f, 6f, 3f)), "The center pivot lifts by half the crate, scaled.");
            var axisX = Vector3.Transform(Vector3.UnitX, pose.Rotation);
            var expected = PlacementMath.LocalXAxis(90f);
            Assert.That(Vector3.Distance(axisX, new Vector3(expected.X, 0f, expected.Y)), Is.LessThan(1e-5));
        }

        [Test]
        public void SlopeObjects_OnTheTerrain_AreSeatedLikeGroundFit()
        {
            var at = new Vector2(5f, 2f);

            var pose = NewSeating().Seat(Fence, new Vector3(at.X, Hill(at).Value, at.Y), onTerrain: true, yawDegrees: 0f, scale: 1f);

            GroundFit.TryPose(at, FenceShape, 0f, 1f, Hill, GroundFit.DefaultMaxTiltDegrees, out var pivot, out var rotation);
            Assert.That(pose.Position, Is.EqualTo(pivot));
            Assert.That(pose.Rotation, Is.EqualTo(rotation));
        }

        [Test]
        public void SlopeObjects_OnOtherObjects_StayUpright()
        {
            var ground = new Vector3(5f, 30f, 2f);

            var pose = NewSeating().Seat(Fence, ground, onTerrain: false, yawDegrees: 30f, scale: 1f);

            Assert.That(pose.Position, Is.EqualTo(ground));
            Assert.That(pose.Rotation, Is.EqualTo(GroundFit.Orient(Vector3.UnitY, 30f)));
        }

        [Test]
        public void SeatOnTerrain_StandsOnTheTerrainHeight()
        {
            var pose = NewSeating().SeatOnTerrain(Crate, new Vector2(4f, 1f), 0f, 1f);

            Assert.That(pose.HasValue, Is.True);
            Assert.That(pose.Value.Position.Y, Is.EqualTo(0.4f * 4f + 10f + 0.5f).Within(1e-5f));
        }

        [Test]
        public void SeatOnTerrain_WithoutTerrain_IsNull()
        {
            var seating = NewSeating(p => null);

            Assert.That(seating.SeatOnTerrain(Crate, Vector2.Zero, 0f, 1f), Is.Null);
        }
    }
}
