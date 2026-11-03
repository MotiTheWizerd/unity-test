using System;
using System.Numerics;
using Forge.Editing.Geometry;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class GroundRectTests
    {
        [Test]
        public void DistanceOutside_IsZeroInsideAndOnTheEdge()
        {
            var rect = new GroundRect(new Vector2(10f, 20f), width: 8f, depth: 4f);

            Assert.That(rect.DistanceOutside(new Vector2(10f, 20f)), Is.EqualTo(0f));
            Assert.That(rect.DistanceOutside(new Vector2(14f, 22f)), Is.EqualTo(0f));
        }

        [Test]
        public void DistanceOutside_MeasuresToTheNearestEdgeOrCorner()
        {
            // Heading 0: width runs east-west, depth north-south.
            var rect = new GroundRect(new Vector2(10f, 20f), width: 8f, depth: 4f);

            Assert.That(rect.DistanceOutside(new Vector2(17f, 20f)), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(rect.DistanceOutside(new Vector2(10f, 25f)), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(rect.DistanceOutside(new Vector2(17f, 26f)), Is.EqualTo(5f).Within(1e-5f), "3-4-5 from the corner (14, 22).");
        }

        [Test]
        public void Heading_TurnsTheRectangleLikeAPlacedObject()
        {
            // Facing east, the width runs across the front: north-south.
            var rect = new GroundRect(Vector2.Zero, width: 20f, depth: 10f, headingDegrees: 90f);

            Assert.That(rect.DistanceOutside(new Vector2(0f, 9f)), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(rect.DistanceOutside(new Vector2(8f, 0f)), Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void Reach_IsTheDistanceToACorner()
        {
            Assert.That(new GroundRect(Vector2.Zero, 6f, 8f, 37f).Reach, Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void SizesMustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GroundRect(Vector2.Zero, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GroundRect(Vector2.Zero, 1f, -1f));
        }
    }
}
