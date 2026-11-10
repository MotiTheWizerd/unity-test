using Forge.UnityEditor.Content;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Content
{
    public class HumanoidTPoseTests
    {
        // A right hand in the T-pose: at the end of the right arm, fingers on along +X, thumb forward (+Z): palm down.
        static readonly Vector3 Hand = new Vector3(0.6f, 1.4f, 0f);
        static readonly Vector3 Knuckle = Hand + new Vector3(0.1f, 0f, 0f);
        static readonly Vector3 Thumb = Hand + new Vector3(0.03f, 0f, 0.04f);

        [Test]
        public void AHandAlreadyInTheTPose_IsLeftAlone()
        {
            var turn = HumanoidTPose.HandTurn(Hand, Knuckle, Thumb, right: true);
            Assert.That(Quaternion.Angle(Quaternion.identity, turn), Is.LessThan(0.5f));
        }

        [Test]
        public void AThumbsUpHand_IsTurnedPalmDown()
        {
            // The same hand rolled a quarter turn about the arm: thumb up, palm facing forward.
            var roll = Quaternion.AngleAxis(-90f, Vector3.right);
            var knuckle = Hand + roll * (Knuckle - Hand);
            var thumb = Hand + roll * (Thumb - Hand);
            Assert.That(thumb.y, Is.GreaterThan(Hand.y), "Thumb up before.");

            var turn = HumanoidTPose.HandTurn(Hand, knuckle, thumb, right: true);
            Assert.That(Quaternion.Angle(Quaternion.identity, turn), Is.EqualTo(90f).Within(0.5f));
            var turnedThumb = turn * (thumb - Hand);
            var turnedKnuckle = turn * (knuckle - Hand);
            Assert.That(turnedThumb.z, Is.GreaterThan(0.03f), "Thumb forward after: palm down.");
            Assert.That(turnedThumb.y, Is.EqualTo(0f).Within(0.005f), "Thumb no longer up.");
            Assert.That(turnedKnuckle.normalized.x, Is.EqualTo(1f).Within(0.01f), "Fingers on along the arm.");
        }

        [Test]
        public void ALeftHand_IsTheRightInAMirror()
        {
            var hand = new Vector3(-Hand.x, Hand.y, Hand.z);
            var knuckle = hand + new Vector3(-0.1f, 0f, 0f);
            var thumb = hand + new Vector3(-0.03f, 0f, 0.04f);
            var turn = HumanoidTPose.HandTurn(hand, knuckle, thumb, right: false);
            Assert.That(Quaternion.Angle(Quaternion.identity, turn), Is.LessThan(0.5f), "Palm down, fingers along −X: already true.");
        }

        [Test]
        public void SplayedToes_AreTurnedForward_AboutTheUpAxis()
        {
            var foot = new Vector3(0.1f, 0.1f, 0f);
            var toes = foot + Quaternion.AngleAxis(22f, Vector3.up) * new Vector3(0f, -0.08f, 0.15f);
            var turn = HumanoidTPose.FootTurn(foot, toes);
            Assert.That(Quaternion.Angle(Quaternion.identity, turn), Is.EqualTo(22f).Within(0.5f));
            var turned = foot + turn * (toes - foot);
            Assert.That(turned.x, Is.EqualTo(foot.x).Within(0.001f), "Toes point forward.");
            Assert.That(turned.y, Is.EqualTo(toes.y).Within(0.001f), "The foot keeps its slope.");
        }

        [Test]
        public void ToesStraightAhead_AreLeftAlone()
        {
            var foot = new Vector3(0.1f, 0.1f, 0f);
            var turn = HumanoidTPose.FootTurn(foot, foot + new Vector3(0f, -0.08f, 0.15f));
            Assert.That(Quaternion.Angle(Quaternion.identity, turn), Is.LessThan(0.01f));
        }
    }
}
