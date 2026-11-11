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
            var wanted = Quaternion.AngleAxis(50f, Vector3.up) * Vector3.forward;
            var turn = HandAim.Turn(Vector3.forward, wanted * 3f);

            AssertNear(turn * Vector3.forward, wanted, "All the way, no further");
        }

        [Test]
        public void WantedFurtherThanAHandTurns_GoesAsFarAsItCan_TowardIt()
        {
            var behind = new Vector3(0.78f, 0.44f, -0.44f).normalized;
            var across = new Vector3(-0.59f, 0.43f, 0.68f).normalized;

            var turn = HandAim.Turn(across, behind);

            Assert.That(Quaternion.Angle(Quaternion.identity, turn), Is.EqualTo(HandAim.MaxTurn).Within(0.1f), "No further than a hand turns");
            Assert.That(Vector3.Angle(turn * across, behind), Is.EqualTo(Vector3.Angle(across, behind) - HandAim.MaxTurn).Within(0.5f),
                "Straight toward the wanted way");
        }

        [Test]
        public void TheForearm_RollsItsShareOfTheTurn_AboutItsLength()
        {
            // The forearm along +X; the turn rolls 90° about it and swings 40° about Y.
            var turn = Quaternion.AngleAxis(40f, Vector3.up) * Quaternion.AngleAxis(90f, Vector3.right);
            var share = HandAim.ForearmPart(turn, Vector3.right * 0.3f);

            share.ToAngleAxis(out var angle, out var axis);
            AssertNear(axis * Mathf.Sign(Vector3.Dot(axis, Vector3.right)), Vector3.right, "It only rolls about the forearm");
            Assert.That(angle, Is.GreaterThan(1f).And.LessThan(90f * HandAim.ForearmShare + 1f), "Its share, no more");
            AssertNear(share * Vector3.right, Vector3.right, "The wrist stays where it is");
            var hand = turn * Quaternion.Inverse(share);
            Assert.That(Quaternion.Angle(hand * share, turn), Is.LessThan(0.01f), "The hand takes the rest: together, the whole turn");
        }

        [Test]
        public void ATurn_WithNoRollAboutTheForearm_LeavesTheForearmBe()
        {
            var turn = Quaternion.AngleAxis(50f, Vector3.up);

            Assert.That(Quaternion.Angle(HandAim.ForearmPart(turn, Vector3.right), Quaternion.identity), Is.LessThan(0.01f));
        }
    }
}
