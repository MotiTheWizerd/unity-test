using Forge.Play;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.Play
{
    public class StanceFinderTests
    {
        [Test]
        public void HipsFaceSquareToTheLineAcrossThem_TurnedRightIsPositive()
        {
            Assert.That(StanceFinder.HipsYaw(Vector3.left, Vector3.right, Vector3.forward, Vector3.up), Is.EqualTo(0f).Within(1e-3f), "Square-on.");
            Assert.That(StanceFinder.HipsYaw(Vector3.forward, Vector3.back, Vector3.forward, Vector3.up), Is.EqualTo(90f).Within(1e-3f),
                "The right hip back, the left ahead: turned to the right.");
            var sideOn = Quaternion.Euler(0f, 55f, 0f);
            Assert.That(StanceFinder.HipsYaw(sideOn * Vector3.left, sideOn * Vector3.right, Vector3.forward, Vector3.up), Is.EqualTo(55f).Within(1e-3f));
            Assert.That(StanceFinder.HipsYaw(sideOn * Vector3.left + Vector3.up * 0.1f, sideOn * Vector3.right, Vector3.forward, Vector3.up),
                Is.EqualTo(55f).Within(1e-3f), "Level: a hip higher than the other doesn't turn them.");
        }

        [Test]
        public void AClipsYaw_IsReadBetweenItsSamples_TheShortWayRound()
        {
            Assert.That(StanceFinder.At(new[] { 0f, 10f, 20f }, 0.25f), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(StanceFinder.At(new[] { 0f, 10f, 20f }, 1f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(StanceFinder.At(new[] { 0f, 10f, 20f }, -1f), Is.EqualTo(0f).Within(1e-4f), "Clamped to the clip.");
            Assert.That(StanceFinder.At(new[] { 170f, -170f }, 0.5f), Is.EqualTo(180f).Within(1e-4f), "Across behind, not through the front.");
            Assert.That(StanceFinder.At(new[] { 7f }, 0.5f), Is.EqualTo(7f));
        }

        [Test]
        public void AnUpperBodyMadeSideOn_OverSquareHips_TurnsBackByTheDifference_AsMuchAsItShows()
        {
            Assert.That(StanceFinder.Keep(57f, 5f, 1f, 90f), Is.EqualTo(52f).Within(1e-4f), "A raised shield over a walk.");
            Assert.That(StanceFinder.Keep(57f, 5f, 0.5f, 90f), Is.EqualTo(26f).Within(1e-4f), "Half shown, half turned.");
            Assert.That(StanceFinder.Keep(57f, 57f, 1f, 90f), Is.EqualTo(0f), "Standing in the stance it was made from: nothing turns.");
            Assert.That(StanceFinder.Keep(57f, 5f, 0f, 90f), Is.EqualTo(0f), "Not shown: nothing turns.");
            Assert.That(StanceFinder.Keep(-170f, 170f, 1f, 90f), Is.EqualTo(20f).Within(1e-4f), "The short way round.");
            Assert.That(StanceFinder.Keep(57f, -70f, 1f, 90f), Is.EqualTo(90f), "No further than a spine twists.");
        }
    }
}
