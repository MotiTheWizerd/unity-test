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
