using System;
using Forge.Data.Sky;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class SkyTimeTests
    {
        static readonly DayCycle Day = new DayCycle(6f, 20f, 60f);

        [TestCase(6f, 0f, 90f)]
        [TestCase(13f, 60f, 180f)]
        [TestCase(20f, 0f, 270f)]
        public void SunPath_ByDay_RisesEast_PeaksSouth_SetsWest(float hour, float height, float bearing)
        {
            var sun = SunPath.At(Day, hour);

            Assert.That(sun.Height, Is.EqualTo(height).Within(1e-3f));
            Assert.That(sun.Bearing, Is.EqualTo(bearing).Within(1e-3f));
        }

        [Test]
        public void SunPath_AtNight_IsBelowTheHorizon_DeepestHalfwayThroughTheNight_InTheNorth()
        {
            // The night runs from 20:00 to 6:00, so its middle is 1:00.
            var middle = SunPath.At(Day, 1f);

            Assert.That(middle.Height, Is.EqualTo(-SunPath.NightDepth).Within(1e-3f));
            Assert.That(middle.Bearing, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(SunPath.At(Day, 23f).IsUp, Is.False);
            Assert.That(SunPath.At(Day, 10f).IsUp, Is.True);
        }

        [Test]
        public void SunPath_IsContinuousThroughSunriseAndSunset()
        {
            foreach (var edge in new[] { Day.Sunrise, Day.Sunset })
            {
                // Three game seconds either side: the sun moves about 13° an hour, so a jump would stand out.
                var before = SunPath.At(Day, edge - 0.001f);
                var after = SunPath.At(Day, edge + 0.001f);
                Assert.That(Math.Abs(after.Height - before.Height), Is.LessThan(0.05f));
                Assert.That(Math.Abs(after.Bearing - before.Bearing), Is.LessThan(0.5f));
            }
        }

        [Test]
        public void DayCycle_RejectsADayThatDoesNotFit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayCycle(20f, 6f, 60f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayCycle(6f, 25f, 60f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayCycle(6f, 20f, 0f));
        }

        [Test]
        public void Clock_RunsAtItsScale_AndStartsANewDayAfterMidnight()
        {
            var clock = new WorldClock(23f) { Running = true };

            // 20 game seconds per real second: 3 real minutes is one game hour.
            clock.Advance(180f);
            Assert.That(clock.Hour, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(clock.Day, Is.EqualTo(1));

            clock.Advance(90f);
            Assert.That(clock.Hour, Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void Clock_Stopped_StaysPut_AndSettingTheHourWraps()
        {
            var clock = new WorldClock(10f);

            clock.Advance(1000f);
            Assert.That(clock.Hour, Is.EqualTo(10f));

            clock.Hour = 25.5f;
            Assert.That(clock.Hour, Is.EqualTo(1.5f));
            clock.Hour = -1f;
            Assert.That(clock.Hour, Is.EqualTo(23f));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Hour = float.NaN);
        }

        [TestCase(0f, "00:00")]
        [TestCase(9.5f, "09:30")]
        [TestCase(23.999f, "23:59")]
        [TestCase(24f, "00:00")]
        public void Clock_FormatsTheHour(float hour, string expected)
        {
            Assert.That(WorldClock.Format(hour), Is.EqualTo(expected));
        }
    }
}
