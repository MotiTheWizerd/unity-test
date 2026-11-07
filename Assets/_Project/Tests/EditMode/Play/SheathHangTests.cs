using System.Collections.Generic;
using Forge.Play;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public class SheathHangTests
    {
        // A man facing +Z takes the hilt at his left hip, holding it blade straight back (as the draw clip's grip lies).
        static readonly Pose Grip = new Pose(new Vector3(-0.15f, 1f, 0.1f), Quaternion.LookRotation(Vector3.left, Vector3.back));

        static readonly Vector3[] NoBody = new Vector3[0];

        static void AssertNear(Vector3 actual, Vector3 expected, string message) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{message}: {actual} is not {expected}");

        [Test]
        public void TheBlade_PointsBackTheWayTheHandPullsItOut_FlatsOutward()
        {
            var pulledTo = Grip.position + new Vector3(0f, 0.3f, 0.1f);

            var sheath = SheathFinder.Hang(Grip, pulledTo, NoBody, 0.5f, Vector3.left);

            AssertNear(sheath.rotation * Vector3.up, new Vector3(0f, -0.3f, -0.1f).normalized, "Down and back, out of the hand's way");
            AssertNear(sheath.rotation * Vector3.forward, Vector3.left, "Flats to the hip");
            AssertNear(sheath.position, Grip.position, "Nothing in the way, so the hilt stays where the hand took it");
        }

        [Test]
        public void TheSheath_StandsOutPastTheBodyBesideTheBlade_ByAScabbard()
        {
            var pulledTo = Grip.position + Vector3.up * 0.3f;
            // The thigh 20 cm down the blade reaches 8 cm further out than the hilt; a point well clear of the blade doesn't count.
            var body = new List<Vector3> { Grip.position + Vector3.down * 0.2f + Vector3.left * 0.08f, Grip.position + Vector3.forward * 0.5f + Vector3.left * 0.3f };

            var sheath = SheathFinder.Hang(Grip, pulledTo, body, 0.5f, Vector3.left);

            AssertNear(sheath.position, Grip.position + Vector3.left * (0.08f + SheathFinder.Scabbard), "Just outside the thigh");
        }

        [Test]
        public void WithoutAPull_TheBladeKeepsTheGripsOwnWay()
        {
            var sheath = SheathFinder.Hang(Grip, Grip.position, NoBody, 0.5f, Vector3.left);

            AssertNear(sheath.rotation * Vector3.up, Vector3.back, "The grip's +Y");
        }
    }
}
