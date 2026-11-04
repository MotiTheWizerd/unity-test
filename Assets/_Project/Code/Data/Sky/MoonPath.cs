using System;

namespace Forge.Data.Sky
{
    /// <summary>
    /// Where a moon stands and how much of it is lit, on a day of the world clock at an hour of a <see cref="DayCycle"/>.
    /// A moon crosses the sky on the sun's arc (<see cref="SunPath.Arc"/>, at its own height), running behind the sun by
    /// its phase: a new moon travels with the sun, a full moon half a day behind it (rising as the sun sets, highest at
    /// night), so it slips a little later every day, as ours does. Its lit share follows from the same phase, and the
    /// sky shader lights its face from the sun's real direction, so the crescent always faces the sun.
    /// </summary>
    public static class MoonPath
    {
        /// <param name="day">The day number of the world clock (<see cref="WorldClock.Day"/>).</param>
        /// <param name="hour">Time of day in hours.</param>
        public static MoonState At(Moon moon, DayCycle cycle, int day, float hour)
        {
            var phase = PhaseOn(moon, day, hour);
            var position = SunPath.Arc(cycle.Sunrise, cycle.Sunset, moon.Highest, hour - phase * WorldClock.HoursPerDay);
            var lit = (1f - (float)Math.Cos(2.0 * Math.PI * phase)) / 2f;
            return new MoonState(moon, position, phase, lit);
        }

        /// <summary>The moon's phase, [0, 1), at <paramref name="hour"/> on <paramref name="day"/>.</summary>
        public static float PhaseOn(Moon moon, int day, float hour)
        {
            var days = day + WorldClock.Wrap(hour) / WorldClock.HoursPerDay;
            var phase = (moon.PhaseAtStart + days / moon.Period) % 1.0;
            if (phase < 0.0)
                phase += 1.0;
            return phase >= 1.0 ? 0f : (float)phase;
        }
    }
}
