using Forge.Play;
using NUnit.Framework;

namespace Forge.Tests.Play
{
    // A blink: the lids quick down, a moment shut, slower up.
    public sealed class EyeBlinkTests
    {
        [Test]
        public void ShutAt_IsOpenAtEitherEnd_ShutBetween_ComingDownQuickerThanItGoesUp()
        {
            Assert.That(EyeBlink.ShutAt(0f), Is.EqualTo(0f));
            Assert.That(EyeBlink.ShutAt(-1f), Is.EqualTo(0f));
            Assert.That(EyeBlink.ShutAt(EyeBlink.Length), Is.EqualTo(0f));
            Assert.That(EyeBlink.ShutAt(EyeBlink.Length + 1f), Is.EqualTo(0f));
            Assert.That(EyeBlink.ShutAt(EyeBlink.Closing + EyeBlink.Held / 2f), Is.EqualTo(1f));
            Assert.That(EyeBlink.Closing, Is.LessThan(EyeBlink.Opening));

            var last = 0f;
            for (var t = 0.001f; t < EyeBlink.Closing; t += 0.005f)
            {
                Assert.That(EyeBlink.ShutAt(t), Is.GreaterThanOrEqualTo(last), "down all the way");
                last = EyeBlink.ShutAt(t);
            }

            last = 1f;
            for (var t = EyeBlink.Closing + EyeBlink.Held; t < EyeBlink.Length; t += 0.005f)
            {
                Assert.That(EyeBlink.ShutAt(t), Is.LessThanOrEqualTo(last), "up all the way");
                last = EyeBlink.ShutAt(t);
            }
        }
    }
}
