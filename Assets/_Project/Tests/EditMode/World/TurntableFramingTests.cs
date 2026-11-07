using Forge.World.Assets;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public class TurntableFramingTests
    {
        const float Fov = 30f;

        [Test]
        public void ALongFlatThing_InAWideView_IsSetByItsReachTiltedIntoTheHeight()
        {
            // A sword lying flat: 0.5 m around, 2 cm up and down.
            var distance = TurntableFraming.Distance(0.5f, 0.02f, Fov, 2.5f, 1f);

            var pitch = TurntableFraming.Pitch * Mathf.Deg2Rad;
            var upDown = (0.02f * Mathf.Cos(pitch) + 0.5f * Mathf.Sin(pitch)) / Mathf.Sin(Fov / 2f * Mathf.Deg2Rad);
            Assert.That(distance, Is.EqualTo(upDown * TurntableFraming.Margin).Within(1e-4f));
        }

        [Test]
        public void ANarrowView_IsSetByTheReachAcross()
        {
            var halfAcross = Mathf.Atan(Mathf.Tan(Fov / 2f * Mathf.Deg2Rad) * 0.4f);

            var distance = TurntableFraming.Distance(0.5f, 0.02f, Fov, 0.4f, 1f);

            Assert.That(distance, Is.EqualTo(0.5f / Mathf.Sin(halfAcross) * TurntableFraming.Margin).Within(1e-4f));
            Assert.That(distance, Is.GreaterThan(TurntableFraming.Distance(0.5f, 0.02f, Fov, 2.5f, 1f)), "Narrower stands further back.");
        }

        [Test]
        public void Distance_GrowsWithTheThing_AndShrinksWithZoom_WithinItsBounds()
        {
            var coin = TurntableFraming.Distance(0.04f, 0.01f, Fov, 1f, 1f);
            var bigCoin = TurntableFraming.Distance(0.4f, 0.1f, Fov, 1f, 1f);

            Assert.That(bigCoin / coin, Is.EqualTo(10f).Within(1e-3f), "Ten times as large stands ten times as far: both fill the view.");
            Assert.That(TurntableFraming.Distance(0.4f, 0.1f, Fov, 1f, 2f), Is.EqualTo(bigCoin / 2f).Within(1e-4f));
            Assert.That(TurntableFraming.Distance(0.4f, 0.1f, Fov, 1f, 100f), Is.EqualTo(bigCoin / TurntableFraming.MaxZoom).Within(1e-4f));
            Assert.That(TurntableFraming.Distance(0.4f, 0.1f, Fov, 1f, 0f), Is.EqualTo(bigCoin / TurntableFraming.MinZoom).Within(1e-4f));
            Assert.That(TurntableFraming.Distance(0f, 0f, Fov, 1f, 1f), Is.GreaterThan(0f), "Something that draws nothing still gets a frame.");
        }

        [Test]
        public void TheCamera_LooksAtTheCentre_FromTheFront_ALittleAbove()
        {
            var centre = new Vector3(1f, 2f, 3f);

            var pose = TurntableFraming.CameraPose(centre, 4f);

            Assert.That(Vector3.Distance(pose.position, centre), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(pose.position.z, Is.LessThan(centre.z), "From the front (−Z).");
            Assert.That(pose.position.y, Is.GreaterThan(centre.y), "From above.");
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, (centre - pose.position).normalized), Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
