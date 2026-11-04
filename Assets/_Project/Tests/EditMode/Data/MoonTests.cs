using System;
using System.Linq;
using Forge.Data.Sky;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class MoonTests
    {
        static readonly DayCycle Day = new DayCycle(6f, 20f, 60f);

        // A cycle so long that the phase barely moves within a day, so a test can hold the moon at one phase.
        static Moon Holding(float phase) => new Moon("Test Moon", period: 100000f, phaseAtStart: phase, size: 2f, highest: 50f);

        [Test]
        public void FullMoon_RisesAsTheSunSets_AndIsHighestAtNight_InTheSouth()
        {
            var full = Holding(0.5f);

            // Half a day behind the sun: up at 18:00, highest at 1:00 (the sun's 13:00), down at 8:00.
            var rising = MoonPath.At(full, Day, 0, 18f);
            var highest = MoonPath.At(full, Day, 0, 1f);
            Assert.That(rising.Position.Height, Is.EqualTo(0f).Within(0.01f));
            Assert.That(rising.Position.Bearing, Is.EqualTo(90f).Within(0.1f));
            Assert.That(highest.Position.Height, Is.EqualTo(full.Highest).Within(0.01f));
            Assert.That(highest.Position.Bearing, Is.EqualTo(180f).Within(0.1f));
            Assert.That(MoonPath.At(full, Day, 0, 12f).Position.IsUp, Is.False, "A full moon is down at noon.");
            Assert.That(highest.Lit, Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void NewMoon_TravelsWithTheSun_Unlit()
        {
            var moon = MoonPath.At(Holding(0f), Day, 0, 13f);
            var sun = SunPath.At(Day, 13f);

            Assert.That(moon.Position.Bearing, Is.EqualTo(sun.Bearing).Within(0.1f));
            Assert.That(moon.Position.IsUp, Is.True);
            Assert.That(moon.Lit, Is.EqualTo(0f).Within(1e-3f));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.5f)]
        [TestCase(0.5f, 1f)]
        [TestCase(0.75f, 0.5f)]
        public void LitShare_FollowsThePhase(float phase, float lit)
        {
            Assert.That(MoonPath.At(Holding(phase), Day, 0, 0f).Lit, Is.EqualTo(lit).Within(1e-3f));
        }

        [Test]
        public void Phase_GoesRoundOnceAPeriod_AndRunsOnThroughTheDay()
        {
            var moon = new Moon("Test Moon", period: 8f, phaseAtStart: 0.25f, size: 2f, highest: 50f);

            Assert.That(MoonPath.PhaseOn(moon, 0, 0f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(MoonPath.PhaseOn(moon, 2, 0f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(MoonPath.PhaseOn(moon, 2, 12f), Is.EqualTo(0.5625f).Within(1e-5f));
            Assert.That(MoonPath.PhaseOn(moon, 8, 0f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(MoonPath.PhaseOn(moon, 6, 0f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Moon_RisesLaterEachDay()
        {
            var moon = new Moon("Test Moon", period: 8f, phaseAtStart: 0.5f, size: 2f, highest: 50f);

            // A day later it is 1/8 of a cycle (3 hours) further behind the sun, so lower in the east at the same hour.
            var today = MoonPath.At(moon, Day, 0, 22f);
            var tomorrow = MoonPath.At(moon, Day, 1, 22f);
            Assert.That(tomorrow.Position.Height, Is.LessThan(today.Position.Height));
        }

        [TestCase(0f, "new")]
        [TestCase(0.97f, "new")]
        [TestCase(0.125f, "waxing crescent")]
        [TestCase(0.25f, "first quarter")]
        [TestCase(0.5f, "full")]
        [TestCase(0.56f, "full")]
        [TestCase(0.75f, "last quarter")]
        [TestCase(0.875f, "waning crescent")]
        public void PhaseName_NamesTheEighthOfTheCycle(float phase, string name)
        {
            Assert.That(MoonPath.At(Holding(phase), Day, 0, 0f).PhaseName, Is.EqualTo(name));
        }

        [Test]
        public void Moon_RejectsWhatCannotBe()
        {
            Assert.Throws<ArgumentException>(() => new Moon(" ", 10f, 0f, 2f, 50f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Moon("Moon", 0.5f, 0f, 2f, 50f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Moon("Moon", 10f, 1f, 2f, 50f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Moon("Moon", 10f, 0f, 0f, 50f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Moon("Moon", 10f, 0f, 2f, 95f));
        }

        [Test]
        public void SkyState_HoldsTheSunEveryMoonAndTheStarsTurn()
        {
            var sky = SkyState.At(Day, Moon.Defaults, 3, 12f);

            Assert.That(sky.Sun.Bearing, Is.EqualTo(SunPath.At(Day, 12f).Bearing));
            Assert.That(sky.Moons.Select(moon => moon.Moon.Name), Is.EqualTo(Moon.Defaults.Select(moon => moon.Name)));
            Assert.That(sky.Moons[0].Phase, Is.EqualTo(MoonPath.PhaseOn(Moon.Defaults[0], 3, 12f)));
            Assert.That(sky.StarTurn, Is.EqualTo(180f).Within(1e-3f));
        }

        [Test]
        public void Clock_DayCanBeSet_ButNotBeforeDayZero()
        {
            var clock = new WorldClock { Day = 5 };

            Assert.That(clock.Day, Is.EqualTo(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Day = -1);
        }
    }
}
