using System;
using System.Numerics;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class GroundFitTests
    {
        static readonly Footprint Fence = new Footprint(3f, 0.2f);

        static GroundPlane Fit(Func<Vector2, float?> heightAt, float yaw = 0f, float maxTilt = GroundFit.DefaultMaxTiltDegrees)
        {
            Assert.That(GroundFit.TryFit(new Vector2(10f, 5f), Fence, yaw, 1f, heightAt, maxTilt, out var plane), Is.True);
            return plane;
        }

        static float TiltDegrees(Vector3 up) => (float)(Math.Acos(up.Y) * 180.0 / Math.PI);

        [Test]
        public void FlatGround_KeepsUpright()
        {
            var plane = Fit(p => 12f);

            Assert.That(Vector3.Distance(plane.Up, Vector3.UnitY), Is.LessThan(1e-5));
            Assert.That(plane.Height, Is.EqualTo(12f));
        }

        [Test]
        public void SlopeAlongTheLength_TiltsSoBothEndsTouch()
        {
            // Rises 0.5 m per meter along +X: the fence's ends sit 1.5 m apart in height.
            var plane = Fit(p => 0.5f * p.X);

            var expected = Vector3.Normalize(new Vector3(-0.5f, 1f, 0f));
            Assert.That(Vector3.Distance(plane.Up, expected), Is.LessThan(1e-4));
            Assert.That(plane.Height, Is.EqualTo(5f).Within(1e-4));
        }

        [Test]
        public void TurnedObject_ReadsTheSlopeAlongItsOwnAxes()
        {
            // Same hill; the fence turned to run along Z still tilts the same way (sideways for it).
            var plane = Fit(p => 0.5f * p.X, yaw: 90f);

            var expected = Vector3.Normalize(new Vector3(-0.5f, 1f, 0f));
            Assert.That(Vector3.Distance(plane.Up, expected), Is.LessThan(1e-4));
        }

        [Test]
        public void SteepGround_IsCappedAtTheMaxTilt()
        {
            var plane = Fit(p => 5f * p.X, maxTilt: 30f);

            Assert.That(TiltDegrees(plane.Up), Is.EqualTo(30f).Within(1e-3));
            Assert.That(plane.Up.X, Is.LessThan(0f), "Still leans away from the uphill side.");
        }

        [Test]
        public void MissingGround_UnderAnySample_Fails()
        {
            var fitted = GroundFit.TryFit(new Vector2(10f, 5f), Fence, 0f, 1f,
                p => p.X > 11f ? (float?)null : 0f, GroundFit.DefaultMaxTiltDegrees, out _);

            Assert.That(fitted, Is.False);
        }

        [Test]
        public void ThinFootprints_SampleAtLeastTheMinimumDistance()
        {
            var reach = 0f;
            GroundFit.TryFit(Vector2.Zero, new Footprint(0.1f, 0.1f), 0f, 1f,
                p => { reach = Math.Max(reach, p.Length()); return 0f; }, 30f, out _);

            Assert.That(reach, Is.EqualTo(GroundFit.MinSampleDistance).Within(1e-5));
        }
    }
}
