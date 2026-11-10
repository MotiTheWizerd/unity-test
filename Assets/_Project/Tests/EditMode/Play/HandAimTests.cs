using Forge.Play;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public class HandAimTests
    {
        static void AssertNear(Vector3 actual, Vector3 expected, string message) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{message}: {actual} is not {expected}");

        [Test]
        public void WantedNothing_TurnsNotAtAll()
        {
            Assert.That(HandAim.Turn(Vector3.left, Vector3.zero), Is.EqualTo(Quaternion.identity));
            Assert.That(HandAim.Turn(Vector3.zero, Vector3.forward), Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void WantedWhole_TakesTheHeldThingAllTheWay()
        {
            var across = new Vector3(-0.72f, 0.17f, 0.68f).normalized;

            var turn = HandAim.Turn(across, Vector3.forward);

            AssertNear(turn * across, Vector3.forward, "The blade points ahead");
        }

        [Test]
        public void WantedHalf_TurnsHalfWay()
        {
            var turn = HandAim.Turn(Vector3.forward, Vector3.right * 0.5f);

            AssertNear(turn * Vector3.forward, new Vector3(1f, 0f, 1f).normalized, "Halfway from ahead to the right");
        }

        [Test]
        public void WantedMoreThanWhole_IsWhole()
        {
            var turn = HandAim.Turn(Vector3.forward, Vector3.right * 3f);

            AssertNear(turn * Vector3.forward, Vector3.right, "All the way, no further");
        }
    }
}
