using Forge.Play;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public class GripFrameTests
    {
        // Arms hanging at the sides of a man facing +Z: fingers down, thumbs forward.
        static readonly Vector3 Down = Vector3.down;
        static readonly Vector3 Forward = Vector3.forward;

        static void AssertNear(Vector3 actual, Vector3 expected, string message) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{message}: {actual} is not {expected}");

        [Test]
        public void TheRightHand_HoldsTheBladeOutPastTheThumb_FlatToThePalm_WhichFacesTheBody()
        {
            var hand = new Vector3(0.3f, 1f, 0f);
            var grip = GripFrame.Hand(hand, hand + Down * 0.05f, hand + Down * 0.04f + Forward * 0.03f, right: true);

            AssertNear(grip.rotation * Vector3.up, Forward, "The blade leaves the fist by the thumb");
            AssertNear(grip.rotation * Vector3.forward, Vector3.left, "The palm faces in, toward the body");
            AssertNear(grip.rotation * Vector3.right, Down, "The guard lies along the fingers");
        }

        [Test]
        public void TheLeftHand_IsTheRightInAMirror()
        {
            var hand = new Vector3(-0.3f, 1f, 0f);
            var grip = GripFrame.Hand(hand, hand + Down * 0.05f, hand + Down * 0.04f + Forward * 0.03f, right: false);

            AssertNear(grip.rotation * Vector3.up, Forward, "The blade leaves the fist by the thumb");
            AssertNear(grip.rotation * Vector3.forward, Vector3.right, "The palm faces in, toward the body");
        }

        [Test]
        public void TheGrip_SitsInThePalm_PastTheKnuckles()
        {
            var grip = GripFrame.Hand(Vector3.zero, Down * 0.05f, Forward * 0.03f, right: true);

            AssertNear(grip.position, Down * (0.05f * GripFrame.Along) + Vector3.left * (0.05f * GripFrame.Into), "Where the fist closes");
        }
    }
}
