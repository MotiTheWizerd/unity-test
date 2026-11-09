using Forge.Play;
using NUnit.Framework;

namespace Forge.Tests.Play
{
    public class SwingWindowTests
    {
        [Test]
        public void TheWindow_IsTheRunAroundTheFastest_ThatKeepsAPartOfItsSpeed()
        {
            // Ten steps: wind-up, a blur from step 4 to 6, then recovery; a late twitch at step 9 doesn't join it.
            var speeds = new[] { 0.1f, 0.2f, 0.5f, 1f, 6f, 10f, 7f, 2f, 0.5f, 5.5f };

            var (from, to) = SwingFinder.Window(speeds, part: 0.5f);

            Assert.That(from, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(to, Is.EqualTo(0.7f).Within(1e-5f));
        }

        [Test]
        public void AClipThatNeverMoves_LandsAnywhere()
        {
            Assert.That(SwingFinder.Window(new float[0]), Is.EqualTo((0f, 1f)));
            Assert.That(SwingFinder.Window(new[] { 0f, 0f }), Is.EqualTo((0f, 1f)));
        }

        [Test]
        public void ABowsDraw_IsOnTheStringWhereTheHandsMeet_AndFullWhereTheyAreFarthestApartAfter()
        {
            // Eleven samples, as the real clip: the hands far apart (the hand at the quiver), meeting at the bow at 0.5,
            // then the pull to full at 0.9, easing off a little at the end.
            var apart = new[] { 1f, 1f, 0.9f, 0.7f, 0.5f, 0.3f, 0.35f, 0.5f, 0.65f, 0.74f, 0.73f };

            var (from, to) = ShotFinder.Window(apart);

            Assert.That(from, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(to, Is.EqualTo(0.9f).Within(1e-5f), "The hands' start apart doesn't count: it is before the arrow is on.");
            Assert.That(ShotFinder.Window(new[] { 1f }), Is.EqualTo((0f, 1f)));
        }

        [Test]
        public void ABlock_IsUpWhereTheShieldHandHasComeMostOfItsWay()
        {
            // Eleven samples, as the real clip: the shield hand still a moment, then up and forward, settling at the end.
            var moved = new[] { 0f, 0f, 0.01f, 0.03f, 0.08f, 0.14f, 0.17f, 0.19f, 0.2f, 0.2f, 0.2f };

            Assert.That(RaiseFinder.Up(moved), Is.EqualTo(0.6f).Within(1e-5f), "0.17 is past 0.8 of the 0.2 it ends at.");
            Assert.That(RaiseFinder.Up(new[] { 0f, 0f }), Is.EqualTo(1f), "A hand that never moves is never up before the end.");
        }

        [Test]
        public void ABowsRelease_LetsGoWhereTheHandsPartFastest()
        {
            // Eleven samples, as the real clip: the draw anchored a little further, the string hand flicking back from
            // 0.3 to 0.4, then the bow brought down (the hands coming together again).
            var apart = new[] { 0.72f, 0.75f, 0.79f, 0.83f, 1.12f, 1.24f, 1.27f, 1.25f, 1.2f, 1.1f, 1.03f };

            Assert.That(ReleaseFinder.Moment(apart), Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(ReleaseFinder.Moment(new[] { 1f, 0.9f, 0.8f }), Is.Zero, "Hands that never part: from the start.");
            Assert.That(ReleaseFinder.Moment(new float[0]), Is.Zero);
        }
    }
}
