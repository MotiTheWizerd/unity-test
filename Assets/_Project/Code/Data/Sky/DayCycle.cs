using System;

namespace Forge.Data.Sky
{
    /// <summary>
    /// The shape of a day: when the sun rises and sets (hours, 0–24) and how high it climbs at noon. One day cycle
    /// serves every worldspace for now; a climate record per worldspace (with seasons) will supply its own.
    /// </summary>
    public readonly struct DayCycle
    {
        /// <summary>Sunrise at 6:00, sunset at 20:00, the sun 60° up at noon (13:00).</summary>
        public static readonly DayCycle Default = new DayCycle(6f, 20f, 60f);

        public DayCycle(float sunrise, float sunset, float noonHeight)
        {
            if (!(sunrise >= 0f && sunrise < sunset && sunset <= WorldClock.HoursPerDay))
                throw new ArgumentOutOfRangeException(nameof(sunrise), $"Sunrise ({sunrise}) must come before sunset ({sunset}), both within the day.");
            if (!(noonHeight > 0f && noonHeight <= 90f))
                throw new ArgumentOutOfRangeException(nameof(noonHeight), noonHeight, "The noon sun's height must be within (0, 90] degrees.");

            Sunrise = sunrise;
            Sunset = sunset;
            NoonHeight = noonHeight;
        }

        /// <summary>Hour the sun rises (crosses the horizon in the east).</summary>
        public float Sunrise { get; }

        /// <summary>Hour the sun sets (crosses the horizon in the west).</summary>
        public float Sunset { get; }

        /// <summary>The sun's height above the horizon halfway between sunrise and sunset, in degrees.</summary>
        public float NoonHeight { get; }
    }
}
