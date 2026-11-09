using System;
using Forge.Game.Cameras;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class DrawZoomTests
    {
        [Test]
        public void NoBowDrawn_TheViewIsAsSet()
        {
            var zoom = new DrawZoom(1.2f, 0.15f);

            zoom.Tick(0f, 1f);

            Assert.That(zoom.Magnification, Is.EqualTo(1f));
        }

        [Test]
        public void TheView_ClosesInWithThePull_Eased_AndComesBackOnceLetGo()
        {
            var zoom = new DrawZoom(1.2f, 0.15f);

            zoom.Tick(1f, 0.15f);
            Assert.That(zoom.Magnification, Is.EqualTo(1f + 0.2f * (1f - (float)Math.Exp(-1))).Within(1e-4f), "Two thirds of the way in its ease.");
            zoom.Tick(1f, 5f);
            Assert.That(zoom.Magnification, Is.EqualTo(1.2f).Within(1e-5f), "Rests at full draw's.");
            zoom.Tick(0.5f, 5f);
            Assert.That(zoom.Magnification, Is.EqualTo(1.1f).Within(1e-4f), "Half drawn, half the zoom.");

            zoom.Tick(0f, 5f);
            Assert.That(zoom.Magnification, Is.EqualTo(1f), "Back to exactly as set.");
        }

        [Test]
        public void WithoutEase_ItFollowsTheDrawAtOnce_AndAResetStopsIt()
        {
            var zoom = new DrawZoom(1.5f, 0f);

            zoom.Tick(2f, 0.01f);
            Assert.That(zoom.Magnification, Is.EqualTo(1.5f).Within(1e-5f), "A pull past full is full.");

            zoom.Reset();
            Assert.That(zoom.Magnification, Is.EqualTo(1f));
        }

        [Test]
        public void AZoom_NarrowsTheViewsHeight_NotItsAngle()
        {
            Assert.That(DrawZoom.FieldOfView(60f, 1f), Is.EqualTo(60f));
            Assert.That(DrawZoom.FieldOfView(90f, 2f), Is.EqualTo(2f * Math.Atan(0.5) * 180.0 / Math.PI).Within(1e-3f));
            Assert.That(DrawZoom.FieldOfView(60f, 1.2f), Is.LessThan(60f).And.GreaterThan(50f));
        }

        [Test]
        public void AZoomOut_OrANegativeEase_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DrawZoom(0.9f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DrawZoom(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DrawZoom(1.2f, -1f));
        }
    }
}
