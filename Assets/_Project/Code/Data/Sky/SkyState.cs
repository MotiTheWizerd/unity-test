using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Sky
{
    /// <summary>
    /// Everything in the sky at one moment: the sun, each moon, and how far the stars have turned. What the sky view
    /// draws and what the agent is told; the game will use the same.
    /// </summary>
    public sealed class SkyState
    {
        SkyState(SkyPosition sun, IReadOnlyList<MoonState> moons, float starTurn)
        {
            Sun = sun;
            Moons = moons;
            StarTurn = starTurn;
        }

        public SkyPosition Sun { get; }

        public IReadOnlyList<MoonState> Moons { get; }

        /// <summary>How far the stars have turned around the pole star, in degrees: once around a day, with the sun.</summary>
        public float StarTurn { get; }

        /// <param name="day">The day number of the world clock (<see cref="WorldClock.Day"/>).</param>
        /// <param name="hour">Time of day in hours.</param>
        public static SkyState At(DayCycle cycle, IReadOnlyList<Moon> moons, int day, float hour)
        {
            if (moons == null)
                throw new ArgumentNullException(nameof(moons));

            return new SkyState(
                SunPath.At(cycle, hour),
                moons.Select(moon => MoonPath.At(moon, cycle, day, hour)).ToArray(),
                360f * WorldClock.Wrap(hour) / WorldClock.HoursPerDay);
        }
    }
}
