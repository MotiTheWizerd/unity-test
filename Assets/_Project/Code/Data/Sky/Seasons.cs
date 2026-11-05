using System;

namespace Forge.Data.Sky
{
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter,
    }

    /// <summary>Where a day falls in the year: the year (from 1), the day of the year (from 1), the season and how far into it.</summary>
    public readonly struct SeasonTime
    {
        static readonly string[] Parts = { "early ", "mid-", "late " };

        public SeasonTime(int year, int dayOfYear, Season season, int part)
        {
            Year = year;
            DayOfYear = dayOfYear;
            Season = season;
            Part = part;
        }

        public int Year { get; }

        public int DayOfYear { get; }

        public Season Season { get; }

        /// <summary>Which third of the season: 0 early, 1 mid, 2 late.</summary>
        public int Part { get; }

        /// <summary>"early spring", "mid-summer", "late winter".</summary>
        public string Describe() => Parts[Part] + Season.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// The year of a climate, the astronomers' way: <c>yearLength</c> days from the first day of spring (when day and
    /// night are even), in four equal seasons. Summer begins on the longest day (midsummer) and winter on the shortest
    /// (midwinter); in between, a day's shape blends smoothly from one to the other, so sunrise creeps a little each day.
    /// Day 0 of the world is the first day of spring.
    /// </summary>
    public static class Seasons
    {
        /// <summary>The shortest year: one day per season.</summary>
        public const int MinYearLength = 4;

        /// <summary>Where day <paramref name="day"/> of the world clock falls in a year of <paramref name="yearLength"/> days.</summary>
        public static SeasonTime On(int day, int yearLength)
        {
            Check(day, yearLength);

            // In whole numbers, so a day exactly on a boundary never rounds into the wrong season or third.
            var dayOfYear = day % yearLength;
            var quarters = dayOfYear * 4;
            var season = quarters / yearLength;
            var part = (quarters - season * yearLength) * 3 / yearLength;
            return new SeasonTime(day / yearLength + 1, dayOfYear + 1, (Season)season, part);
        }

        /// <summary>
        /// How summery day <paramref name="day"/> is: 1 at midsummer (the first day of summer), 0 at midwinter (the
        /// first day of winter), ½ on the first days of spring and autumn.
        /// </summary>
        public static float Summerness(int day, int yearLength)
        {
            Check(day, yearLength);

            var intoYear = (double)(day % yearLength) / yearLength;
            return (float)((1.0 + Math.Sin(2.0 * Math.PI * intoYear)) / 2.0);
        }

        /// <summary>The shape of day <paramref name="day"/>: <paramref name="winter"/>'s at midwinter, <paramref name="summer"/>'s at midsummer.</summary>
        public static DayCycle DayOn(int day, int yearLength, DayCycle summer, DayCycle winter) =>
            DayCycle.Between(winter, summer, Summerness(day, yearLength));

        static void Check(int day, int yearLength)
        {
            if (day < 0)
                throw new ArgumentOutOfRangeException(nameof(day), day, "The day can't be before day 0.");
            if (yearLength < MinYearLength)
                throw new ArgumentOutOfRangeException(nameof(yearLength), yearLength, $"A year has at least {MinYearLength} days, one per season.");
        }
    }
}
