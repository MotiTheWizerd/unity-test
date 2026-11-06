using Forge.Game.Hud;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class BarFadeTests
    {
        [Test]
        public void AFullBar_NotYetSeen_IsHidden()
        {
            var fade = new BarFade();

            fade.Tick(1f, 0.1f);

            Assert.That(fade.Opacity, Is.Zero);
        }

        [Test]
        public void ABarNotFull_Shows_ThenLingers_ThenFades()
        {
            var fade = new BarFade();
            fade.Tick(0.5f, 0.1f);
            Assert.That(fade.Opacity, Is.EqualTo(1f));

            fade.Tick(1f, BarFade.Linger);
            Assert.That(fade.Opacity, Is.EqualTo(1f), "Lingers once full.");

            fade.Tick(1f, BarFade.FadeTime / 2f);
            Assert.That(fade.Opacity, Is.EqualTo(0.5f).Within(1e-4f));

            fade.Tick(1f, BarFade.FadeTime);
            Assert.That(fade.Opacity, Is.Zero);
        }

        [Test]
        public void AFlash_Shows_AndBlinks_ThenStops()
        {
            var fade = new BarFade();

            fade.Flash();
            fade.Tick(1f, 0.01f);
            Assert.That((fade.Opacity, fade.FlashOn), Is.EqualTo((1f, true)));

            fade.Tick(1f, BarFade.BlinkTime);
            Assert.That(fade.FlashOn, Is.False, "The second blink is dark.");

            fade.Tick(1f, BarFade.FlashTime);
            Assert.That(fade.FlashOn, Is.False);
            Assert.That(fade.Opacity, Is.EqualTo(1f), "Still lingering after the flash.");
        }
    }
}
