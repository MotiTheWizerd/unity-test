using System;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Sky;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class ClimateTests
    {
        static readonly DayCycle Summer = new DayCycle(4f, 22f, 70f);
        static readonly DayCycle Winter = new DayCycle(8f, 16f, 20f);

        public static ClimateRecord Northern(string ns = "core") =>
            new ClimateRecord(ClimateRecord.IdFor(ns, "northern"), "Northern", 120, Summer, Winter, Moon.Defaults);

        [TestCase(0, 1, 1, Season.Spring, 0)]
        [TestCase(10, 1, 11, Season.Spring, 1)]
        [TestCase(29, 1, 30, Season.Spring, 2)]
        [TestCase(30, 1, 31, Season.Summer, 0)]
        [TestCase(75, 1, 76, Season.Autumn, 1)]
        [TestCase(119, 1, 120, Season.Winter, 2)]
        [TestCase(120, 2, 1, Season.Spring, 0)]
        public void Seasons_SplitTheYearIntoFourEqualSeasons_InThirds(int day, int year, int dayOfYear, Season season, int part)
        {
            var time = Seasons.On(day, 120);

            Assert.That((time.Year, time.DayOfYear, time.Season, time.Part), Is.EqualTo((year, dayOfYear, season, part)));
        }

        [Test]
        public void Seasons_DescribeThePartOfTheSeason()
        {
            Assert.That(Seasons.On(40, 120).Describe(), Is.EqualTo("mid-summer"));
            Assert.That(Seasons.On(90, 120).Describe(), Is.EqualTo("early winter"));
        }

        [TestCase(0, 0.5f)]
        [TestCase(30, 1f)]
        [TestCase(60, 0.5f)]
        [TestCase(90, 0f)]
        [TestCase(120, 0.5f)]
        public void Summerness_PeaksAtMidsummer_AndBottomsOutAtMidwinter(int day, float expected)
        {
            Assert.That(Seasons.Summerness(day, 120), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void DayOn_IsTheSummerDayAtMidsummer_TheWinterDayAtMidwinter_AndEvenInBetween()
        {
            var climate = Northern();

            var midsummer = climate.DayOn(30);
            var midwinter = climate.DayOn(90);
            var spring = climate.DayOn(0);
            Assert.That((midsummer.Sunrise, midsummer.Sunset, midsummer.NoonHeight), Is.EqualTo((4f, 22f, 70f)));
            Assert.That((midwinter.Sunrise, midwinter.Sunset, midwinter.NoonHeight), Is.EqualTo((8f, 16f, 20f)));
            Assert.That((spring.Sunrise, spring.Sunset, spring.NoonHeight), Is.EqualTo((6f, 19f, 45f)));
            Assert.That(climate.DayOn(31).DaylightHours(), Is.LessThan(midsummer.DaylightHours()), "Days shorten after midsummer.");
        }

        [Test]
        public void DayCycle_Between_BlendsEveryPart_AndClampsT()
        {
            var halfway = DayCycle.Between(Winter, Summer, 0.5f);

            Assert.That((halfway.Sunrise, halfway.Sunset, halfway.NoonHeight), Is.EqualTo((6f, 19f, 45f)));
            Assert.That(DayCycle.Between(Winter, Summer, 2f).Sunrise, Is.EqualTo(Summer.Sunrise));
        }

        [Test]
        public void Climate_RejectsWhatCannotBe()
        {
            var id = ClimateRecord.IdFor("core", "x");
            Assert.Throws<ArgumentException>(() => new ClimateRecord(RecordId.Parse("core:x"), "X", 120, Summer, Winter, Moon.Defaults));
            Assert.Throws<ArgumentException>(() => new ClimateRecord(id, " ", 120, Summer, Winter, Moon.Defaults));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClimateRecord(id, "X", 3, Summer, Winter, Moon.Defaults));
            Assert.Throws<ArgumentNullException>(() => new ClimateRecord(id, "X", 120, null, Winter, Moon.Defaults));
            Assert.Throws<ArgumentNullException>(() => new ClimateRecord(id, "X", 120, Summer, Winter, null));
            var fiveMoons = new[] { Moon.Defaults[0], Moon.Defaults[0], Moon.Defaults[0], Moon.Defaults[0], Moon.Defaults[0] };
            Assert.Throws<ArgumentException>(() => new ClimateRecord(id, "X", 120, Summer, Winter, fiveMoons));
            Assert.That(new ClimateRecord(id, "Moonless", 120, Summer, Winter, Array.Empty<Moon>()).Moons, Is.Empty);
        }

        [Test]
        public void Space_KeepsItsClimate_ThroughOtherChanges_AndInteriorsHaveNone()
        {
            var climate = Northern().Id;
            var space = new SpaceRecord(TestRecords.Space, "Main", SpaceKind.Worldspace, 50f, climate: climate);

            Assert.That(space.WithWaterLevel(40f).Climate, Is.EqualTo(climate));
            Assert.That(space.WithClimate(null).Climate, Is.Null);
            Assert.That(new SpaceRecord(SpaceRecord.IdFor("core", "cave"), "Cave", SpaceKind.Interior, climate: climate).Climate, Is.Null);
        }
    }
}
