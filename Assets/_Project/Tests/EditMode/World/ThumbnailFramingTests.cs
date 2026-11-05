using Forge.World.Assets;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public class ThumbnailFramingTests
    {
        [Test]
        public void CameraLooksAtTheBoxCentreAlongTheViewDirection()
        {
            var bounds = new Bounds(new Vector3(3f, 1f, -2f), new Vector3(2f, 4f, 2f));

            var pose = ThumbnailFraming.CameraPose(bounds, out _, out _, out _);

            var toCentre = (bounds.center - pose.position).normalized;
            Assert.That(Vector3.Dot(toCentre, ThumbnailFraming.ViewDirection), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, ThumbnailFraming.ViewDirection), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void EveryCornerFallsInsideTheView()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(1f, 6f, 2f));

            var pose = ThumbnailFraming.CameraPose(bounds, out var size, out var near, out var far);

            var inverse = Quaternion.Inverse(pose.rotation);
            foreach (var corner in Corners(bounds))
            {
                var local = inverse * (corner - pose.position);
                Assert.That(Mathf.Abs(local.x), Is.LessThanOrEqualTo(size));
                Assert.That(Mathf.Abs(local.y), Is.LessThanOrEqualTo(size));
                Assert.That(local.z, Is.GreaterThan(near).And.LessThan(far));
            }
        }

        [Test]
        public void TheLargerExtentSetsTheSize_WithAMargin()
        {
            var tall = new Bounds(Vector3.zero, new Vector3(1f, 10f, 1f));
            var wide = new Bounds(Vector3.zero, new Vector3(10f, 1f, 1f));

            ThumbnailFraming.CameraPose(tall, out var tallSize, out _, out _);
            ThumbnailFraming.CameraPose(wide, out var wideSize, out _, out _);

            // Height projects straight; width is foreshortened by the three-quarter turn, so it needs less.
            Assert.That(tallSize, Is.GreaterThan(5f).And.LessThan(5f * ThumbnailFraming.Margin + 1e-3f));
            Assert.That(wideSize, Is.GreaterThan(1f).And.LessThan(tallSize));
        }

        [Test]
        public void AnEmptyBoxStillGetsAFrame()
        {
            ThumbnailFraming.CameraPose(new Bounds(Vector3.zero, Vector3.zero), out var size, out var near, out var far);

            Assert.That(size, Is.GreaterThan(0f));
            Assert.That(far, Is.GreaterThan(near));
        }

        static Vector3[] Corners(Bounds b) => new[]
        {
            new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(b.max.x, b.min.y, b.min.z),
            new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z),
            new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.min.y, b.max.z),
            new Vector3(b.min.x, b.max.y, b.max.z), new Vector3(b.max.x, b.max.y, b.max.z),
        };
    }
}
