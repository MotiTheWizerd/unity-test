using System;
using System.Numerics;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class GroundPoseTests
    {
        static readonly PlacementShape Fence = new PlacementShape(new Footprint(3f, 0.2f), 0f);

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(215f)]
        public void Orient_OnFlatGround_IsTheEnginesYaw(float yaw)
        {
            // The engine turns local +X to (cos, 0, -sin) for a yaw around +Y.
            var axisX = Vector3.Transform(Vector3.UnitX, GroundFit.Orient(Vector3.UnitY, yaw));
            var expected = PlacementMath.LocalXAxis(yaw);

            Assert.That(Vector3.Distance(axisX, new Vector3(expected.X, 0f, expected.Y)), Is.LessThan(1e-5));
        }

        [Test]
        public void Orient_TiltsTheObjectsUpToTheGround()
        {
            var up = Vector3.Normalize(new Vector3(-0.5f, 1f, 0.2f));

            var rotated = Vector3.Transform(Vector3.UnitY, GroundFit.Orient(up, 70f));

            Assert.That(Vector3.Distance(rotated, up), Is.LessThan(1e-5));
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(300f)]
        public void HeadingOf_ReadsTheYawBack_EvenWhenTilted(float yaw)
        {
            var tilted = GroundFit.Orient(Vector3.Normalize(new Vector3(0.3f, 1f, -0.2f)), yaw);

            var heading = PlacementMath.HeadingOf(tilted);

            var difference = Math.Abs(((heading - yaw) % 360f + 540f) % 360f - 180f);
            Assert.That(difference, Is.LessThan(3f), "A small tilt barely moves the heading.");
        }

        [Test]
        public void TryPose_OnASlope_PutsBothFenceEndsOnTheGround()
        {
            Func<Vector2, float?> ground = p => 0.4f * p.X + 10f;

            Assert.That(GroundFit.TryPose(new Vector2(5f, 2f), Fence, 0f, 1f, ground, 35f, out var pivot, out var rotation), Is.True);

            foreach (var end in new[] { 1.5f, -1.5f })
            {
                var world = pivot + Vector3.Transform(new Vector3(end, 0f, 0f), rotation);
                Assert.That(world.Y, Is.EqualTo(ground(new Vector2(world.X, world.Z)).Value).Within(0.02f), $"end {end}");
            }
        }

        [Test]
        public void TryPose_LiftsCenterPivotsAlongTheTiltedUp()
        {
            var ball = new PlacementShape(new Footprint(1f, 1f), 0.5f);

            GroundFit.TryPose(Vector2.Zero, ball, 0f, 2f, p => 0f, 35f, out var pivot, out _);

            Assert.That(pivot, Is.EqualTo(new Vector3(0f, 1f, 0f)));
        }
    }
}
