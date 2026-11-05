using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Sky;

namespace Forge.Data.Records
{
    /// <summary>
    /// The sky's rules for the worldspaces that use it, like a Bethesda climate: how long a year is, the shape of a
    /// midsummer and a midwinter day (the days between blend from one to the other through the seasons), and the
    /// moons. One climate serves many worldspaces (<see cref="SpaceRecord.Climate"/>), and a mod changes them all by
    /// overriding it. Later it will hold the weather too.
    /// </summary>
    public sealed class ClimateRecord : Record
    {
        /// <summary>Name prefix of climate records, e.g. "forge.core:climate/temperate".</summary>
        public const string NamePrefix = "climate/";

        public const int MaxYearLength = 1000;

        /// <param name="moons">The moons in the sky, at most <see cref="Moon.MaxPerSky"/>; empty for none.</param>
        public ClimateRecord(RecordId id, string displayName, int yearLength, DayCycle summer, DayCycle winter, IReadOnlyList<Moon> moons)
            : base(id)
        {
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Climate ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (yearLength < Seasons.MinYearLength || yearLength > MaxYearLength)
                throw new ArgumentOutOfRangeException(nameof(yearLength), yearLength, $"A year lasts [{Seasons.MinYearLength}, {MaxYearLength}] days.");
            if (moons == null)
                throw new ArgumentNullException(nameof(moons), "A climate lists its moons (an empty list for none).");
            if (moons.Count > Moon.MaxPerSky)
                throw new ArgumentException($"A sky holds at most {Moon.MaxPerSky} moons, not {moons.Count}.", nameof(moons));
            if (moons.Any(moon => moon == null))
                throw new ArgumentException("A climate's moons can't be missing.", nameof(moons));

            DisplayName = displayName;
            YearLength = yearLength;
            Summer = summer ?? throw new ArgumentNullException(nameof(summer));
            Winter = winter ?? throw new ArgumentNullException(nameof(winter));
            Moons = moons.ToArray();
        }

        public string DisplayName { get; }

        /// <summary>Days from the first day of spring to the next.</summary>
        public int YearLength { get; }

        /// <summary>The shape of midsummer's day, the longest.</summary>
        public DayCycle Summer { get; }

        /// <summary>The shape of midwinter's day, the shortest.</summary>
        public DayCycle Winter { get; }

        public IReadOnlyList<Moon> Moons { get; }

        /// <summary>The shape of day <paramref name="day"/> of the world clock.</summary>
        public DayCycle DayOn(int day) => Seasons.DayOn(day, YearLength, Summer, Winter);

        /// <summary>Where day <paramref name="day"/> of the world clock falls in this climate's year.</summary>
        public SeasonTime SeasonOn(int day) => Seasons.On(day, YearLength);

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);
    }
}
