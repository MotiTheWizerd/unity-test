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
    }
}
