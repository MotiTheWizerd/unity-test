using System;
using System.Collections.Generic;

namespace Forge.Data.Sky
{
    /// <summary>
    /// A moon in the sky: how many days it takes to go from new to full and back (<see cref="Period"/>), where in that
    /// cycle it is on day 0 (<see cref="PhaseAtStart"/>), how big it looks and how high it climbs. Where it stands and
    /// how much of it is lit come from <see cref="MoonPath"/>. <see cref="Defaults"/> serve every worldspace until
    /// climate records per worldspace carry their own.
    /// </summary>
    public readonly struct Moon
    {
        /// <summary>Two moons: a large one on a 24-day cycle, nearly full on day 0, and a small, quicker one.</summary>
        public static readonly IReadOnlyList<Moon> Defaults = new[]
        {
            new Moon("Greater Moon", period: 24f, phaseAtStart: 0.45f, size: 4.5f, highest: 50f),
            new Moon("Lesser Moon", period: 10f, phaseAtStart: 0.3f, size: 1.8f, highest: 65f),
        };

        public Moon(string name, float period, float phaseAtStart, float size, float highest)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A moon needs a name.", nameof(name));
            if (!(period >= 1f) || float.IsInfinity(period))
                throw new ArgumentOutOfRangeException(nameof(period), period, "A moon's cycle must last at least a day.");
            if (!(phaseAtStart >= 0f && phaseAtStart < 1f))
                throw new ArgumentOutOfRangeException(nameof(phaseAtStart), phaseAtStart, "A phase is within [0, 1): 0 new, 0.5 full.");
            if (!(size > 0f && size <= 30f))
                throw new ArgumentOutOfRangeException(nameof(size), size, "A moon's size must be within (0, 30] degrees.");
            if (!(highest > 0f && highest <= 90f))
                throw new ArgumentOutOfRangeException(nameof(highest), highest, "A moon's highest point must be within (0, 90] degrees.");

            Name = name;
            Period = period;
            PhaseAtStart = phaseAtStart;
            Size = size;
            Highest = highest;
        }

        public string Name { get; }

        /// <summary>Days from new moon to the next new moon.</summary>
        public float Period { get; }

        /// <summary>The phase at 0:00 on day 0: 0 new, 0.25 first quarter, 0.5 full, 0.75 last quarter.</summary>
        public float PhaseAtStart { get; }

        /// <summary>How wide the moon looks, in degrees (the sun is 1.4°).</summary>
        public float Size { get; }

        /// <summary>The moon's height above the horizon at the top of its arc, in degrees.</summary>
        public float Highest { get; }
    }
}
