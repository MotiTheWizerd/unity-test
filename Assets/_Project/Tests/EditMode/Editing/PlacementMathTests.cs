using System;
using System.Numerics;
using Forge.Editing.Placement;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class PlacementMathTests
    {
        [TestCase(0f, 90f)]
        [TestCase(45f, 300f)]
        [TestCase(270f, 10f)]
        public void WithHeading_TurnsToTheHeading(float from, float to)
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, from * (float)(Math.PI / 180.0));

            var turned = PlacementMath.WithHeading(rotation, to);

            Assert.That(AngleBetween(PlacementMath.HeadingOf(turned), to), Is.LessThan(1e-2f));
        }

        [Test]
        public void WithHeading_KeepsTheTilt()
        {
            // Tilted 20 degrees about X (as on a slope), then turned.
            var tilted = Quaternion.Concatenate(Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.35f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f));
            var upBefore = Vector3.Transform(Vector3.UnitY, tilted);

            var turned = PlacementMath.WithHeading(tilted, 200f);

            var upAfter = Vector3.Transform(Vector3.UnitY, turned);
            Assert.That(AngleBetween(PlacementMath.HeadingOf(turned), 200f), Is.LessThan(1e-2f));
            Assert.That(upAfter.Y, Is.EqualTo(upBefore.Y).Within(1e-4f), "The lean from upright is the same.");
            Assert.That(turned.Length(), Is.EqualTo(1f).Within(1e-5f));
        }

        static float AngleBetween(float a, float b)
        {
            var d = Math.Abs(PlacementMath.NormalizeDegrees(a) - PlacementMath.NormalizeDegrees(b));
            return Math.Min(d, 360f - d);
        }
    }
}
