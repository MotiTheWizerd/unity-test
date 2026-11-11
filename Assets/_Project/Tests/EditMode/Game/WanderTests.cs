using System;
using System.Numerics;
using Forge.Game.Minds;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class WanderTests
    {
        const float Frame = 0.02f;
        static readonly WanderSettings Settings = new WanderSettings(radius: 5f, speed: 1f, minPause: 1f, maxPause: 2f);

        static Wander Make(int seed = 1) => new Wander(new Vector2(10f, -4f), Settings, new Random(seed));

        // Walks it as a body would, frame by frame, for up to `seconds`; where it ends.
        static Vector2 Run(Wander wander, Vector2 position, float seconds, Func<bool> until = null)
        {
            for (var t = 0f; t < seconds && (until == null || !until()); t += Frame)
                position += wander.Step(position, Frame) * Frame;
            return position;
        }

        [Test]
        public void StandsAtFirst_ThenSetsOff()
        {
            var wander = Make();

            Assert.That(wander.Target, Is.Null);
            Run(wander, wander.Home, Settings.MaxPause + Frame * 2, () => wander.Target.HasValue);
            Assert.That(wander.Target, Is.Not.Null);
        }

        [Test]
        public void WalksAtItsSpeed_ToSpotsWithinItsRadiusOfHome()
        {
            var wander = Make();
            var position = wander.Home;
            for (var i = 0; i < 2000; i++)
            {
                var velocity = wander.Step(position, Frame);
                Assert.That(velocity.Length(), Is.LessThanOrEqualTo(Settings.Speed + 1e-3f));
                if (wander.Target.HasValue)
                    Assert.That(Vector2.Distance(wander.Target.Value, wander.Home), Is.LessThanOrEqualTo(Settings.Radius + 1e-3f));
                position += velocity * Frame;
            }

            Assert.That(Vector2.Distance(position, wander.Home), Is.LessThanOrEqualTo(Settings.Radius + 1e-3f));
        }

        [Test]
        public void ArrivesOnTheSpot_AndStandsThere()
        {
            var wander = Make();
            var position = Run(wander, wander.Home, 10f, () => wander.Target.HasValue);
            var spot = wander.Target.Value;

            position = Run(wander, position, 20f, () => !wander.Target.HasValue);

            Assert.That(wander.Target, Is.Null);
            Assert.That(Vector2.Distance(position, spot), Is.LessThan(1e-3f));
            Assert.That(wander.Step(position, Frame), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void PicksSpotsWorthTheWalk()
        {
            var wander = Make();
            var position = wander.Home;
            for (var walks = 0; walks < 20; walks++)
            {
                position = Run(wander, position, 10f, () => wander.Target.HasValue);
                Assert.That(Vector2.Distance(wander.Target.Value, position), Is.GreaterThanOrEqualTo(Settings.Radius / 3f - 1e-3f));
                position = Run(wander, position, 20f, () => !wander.Target.HasValue);
            }
        }

        [Test]
        public void Blocked_StopsAndStands_ThenGoesElsewhere()
        {
            var wander = Make();
            var position = Run(wander, wander.Home, 10f, () => wander.Target.HasValue);
            var first = wander.Target.Value;

            wander.Blocked();

            Assert.That(wander.Target, Is.Null);
            Assert.That(wander.Step(position, Frame), Is.EqualTo(Vector2.Zero));
            Run(wander, position, Settings.MaxPause + Frame * 2, () => wander.Target.HasValue);
            Assert.That(wander.Target, Is.Not.EqualTo(first));
        }

        [Test]
        public void GettingNowhere_GivesTheWalkUp_ThenGoesElsewhere()
        {
            var wander = Make();
            var position = Run(wander, wander.Home, 10f, () => wander.Target.HasValue);
            var first = wander.Target.Value;

            // Something stands in the way: the body stays where it is, however it is sent.
            var seconds = 0f;
            while (wander.Target.HasValue && seconds < 2f)
            {
                wander.Step(position, Frame);
                seconds += Frame;
            }

            Assert.That(wander.Target, Is.Null);
            Assert.That(seconds, Is.EqualTo(Wander.StuckSeconds).Within(Frame * 2f));
            Run(wander, position, Settings.MaxPause + Frame * 2, () => wander.Target.HasValue);
            Assert.That(wander.Target, Is.Not.EqualTo(first));
        }

        [Test]
        public void SlowedButGettingThere_KeepsWalking()
        {
            var wander = Make();
            var position = Run(wander, wander.Home, 10f, () => wander.Target.HasValue);
            var spot = wander.Target.Value;

            // Up a slope, round a shoulder: half as far as sent, but on its way.
            for (var t = 0f; t < 2f; t += Frame)
                position += wander.Step(position, Frame) * Frame * 0.5f;

            Assert.That(wander.Target, Is.EqualTo(spot));
        }

        [Test]
        public void TheSameDice_WalkTheSameWay()
        {
            var a = Make(seed: 7);
            var b = Make(seed: 7);

            Assert.That(Run(a, a.Home, 30f), Is.EqualTo(Run(b, b.Home, 30f)));
        }

        [Test]
        public void WithSpeed_ChangesOnlyTheSpeed()
        {
            var faster = Settings.WithSpeed(1.6f);

            Assert.That(faster.Speed, Is.EqualTo(1.6f));
            Assert.That((faster.Radius, faster.MinPause, faster.MaxPause), Is.EqualTo((Settings.Radius, Settings.MinPause, Settings.MaxPause)));
        }

        [Test]
        public void Settings_RefuseNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WanderSettings(0f, 1f, 1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WanderSettings(5f, -1f, 1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WanderSettings(5f, 1f, 3f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make().Step(Vector2.Zero, 0f));
        }
    }
}
