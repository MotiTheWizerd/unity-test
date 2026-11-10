using System;
using System.Linq;
using System.Numerics;
using Forge.Editing.Geometry;
using Forge.Editing.Terrain;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class GroundShapeTests
    {
        [Test]
        public void ABandAroundOnePoint_IsADisc()
        {
            var disc = GroundShape.Band(new[] { new Vector2(10f, 10f) }, 8f);

            Assert.That(disc.SignedDistance(new Vector2(10f, 10f)), Is.EqualTo(-4f).Within(1e-5f), "The center: 4 m in.");
            Assert.That(disc.SignedDistance(new Vector2(14f, 10f)), Is.EqualTo(0f).Within(1e-5f), "On the rim.");
            Assert.That(disc.SignedDistance(new Vector2(10f, 20f)), Is.EqualTo(6f).Within(1e-5f));
            Assert.That((disc.Min, disc.Max), Is.EqualTo((new Vector2(6f, 6f), new Vector2(14f, 14f))));
        }

        [Test]
        public void ABand_IsMeasuredFromItsPath_RoundedAtTheEnds()
        {
            var band = GroundShape.Band(new[] { new Vector2(0f, 0f), new Vector2(20f, 0f) }, 4f);

            Assert.That(band.SignedDistance(new Vector2(10f, 0f)), Is.EqualTo(-2f).Within(1e-5f));
            Assert.That(band.SignedDistance(new Vector2(10f, 5f)), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(band.SignedDistance(new Vector2(23f, 4f)), Is.EqualTo(3f).Within(1e-5f), "5 m from the end, round it.");
            Assert.That(band.Span, Is.EqualTo(new Vector2(24f, 4f)));
        }

        [Test]
        public void AnOffsetPath_RunsBesideIt_ToItsRightAsWalked()
        {
            var north = new[] { new Vector2(0f, 0f), new Vector2(0f, 10f) };

            Assert.That(GroundShape.OffsetPath(north, 2f), Is.EqualTo(new[] { new Vector2(2f, 0f), new Vector2(2f, 10f) }), "Walking north, right is east.");
            Assert.That(GroundShape.OffsetPath(north, -2f), Is.EqualTo(new[] { new Vector2(-2f, 0f), new Vector2(-2f, 10f) }));
        }

        [Test]
        public void AnOffsetPath_KeepsItsDistance_RoundABend()
        {
            // North, then east: the outside of the bend is to the left (west, then north).
            var bend = new[] { new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(10f, 10f) };

            var left = GroundShape.OffsetPath(bend, -1f);

            Assert.That(left[0], Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(Vector2.Distance(left[1], new Vector2(-1f, 11f)), Is.LessThan(1e-5f), "The corner moves out along the bend's middle.");
            Assert.That(Vector2.Distance(left[2], new Vector2(10f, 11f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void AnOffsetPath_DoesNotSpike_AtAHairpin_AndStaysClosed()
        {
            var hairpin = new[] { new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(0.5f, 0f) };
            var corner = GroundShape.OffsetPath(hairpin, 1f)[1];
            Assert.That(Vector2.Distance(corner, hairpin[1]), Is.LessThanOrEqualTo(GroundShape.MaxMiter + 1e-4f));

            var square = new[] { new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(10f, 10f), new Vector2(10f, 0f), new Vector2(0f, 0f) };
            var inside = GroundShape.OffsetPath(square, 1f);
            Assert.That(inside[0], Is.EqualTo(inside[4]), "A ring stays closed.");
            Assert.That(Vector2.Distance(inside[0], new Vector2(1f, 1f)), Is.LessThan(1e-5f), "Its first corner is mitred like the others.");
        }

        [Test]
        public void RectanglesAndPolygons_AreNegativeInside()
        {
            var rect = GroundShape.Rect(new GroundRect(new Vector2(10f, 10f), 10f, 4f));
            var triangle = GroundShape.Polygon(new[] { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(0f, 10f) });

            Assert.That(rect.SignedDistance(new Vector2(10f, 10f)), Is.EqualTo(-2f).Within(1e-5f), "2 m in from the long sides.");
            Assert.That(rect.SignedDistance(new Vector2(18f, 10f)), Is.EqualTo(3f).Within(1e-5f));
            Assert.That(triangle.SignedDistance(new Vector2(1f, 1f)), Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(triangle.SignedDistance(new Vector2(-2f, 5f)), Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void Shapes_RefuseWhatIsNoShape()
        {
            Assert.Throws<ArgumentException>(() => GroundShape.Band(Array.Empty<Vector2>(), 4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => GroundShape.Band(new[] { Vector2.Zero }, 0f));
            Assert.Throws<ArgumentException>(() => GroundShape.Polygon(new[] { Vector2.Zero, Vector2.One }));
        }

        [Test]
        public void TerrainNoise_VariesWithinItsRange_SmoothlyBetweenNeighbours()
        {
            var values = Enumerable.Range(0, 400).Select(i => TerrainNoise.At(new Vector2(i * 1.7f, i * 0.9f), 20f, 4, 1)).ToList();

            Assert.That(values, Is.All.InRange(-1f, 1f));
            Assert.That(values.Max() - values.Min(), Is.GreaterThan(0.5f), "It varies.");
            Assert.That(Math.Abs(TerrainNoise.At(new Vector2(30f, 70f), 20f, 4, 1) - TerrainNoise.At(new Vector2(30.1f, 70f), 20f, 4, 1)),
                Is.LessThan(0.05f), "A tenth of a meter on, it has hardly changed.");
            Assert.That(TerrainNoise.At(new Vector2(30f, 70f), 20f, 4, 1), Is.Not.EqualTo(TerrainNoise.At(new Vector2(30f, 70f), 20f, 4, 2)),
                "Another seed, another pattern.");
        }
    }
}
