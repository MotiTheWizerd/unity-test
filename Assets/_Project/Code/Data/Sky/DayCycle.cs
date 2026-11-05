using System;

namespace Forge.Data.Sky
{
    /// <summary>
    /// The shape of a day: when the sun rises and sets (hours, 0–24) and how high it climbs at noon. A climate holds
    /// one for midsummer and one for midwinter and blends between them over the year (<see cref="Between"/>); a
    /// worldspace without a climate has <see cref="Default"/> every day.
    /// </summary>
    public sealed class DayCycle
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

        /// <summary>Hours from sunrise to sunset. A method, not a property: serializers write every property, and this is not data.</summary>
        public float DaylightHours() => Sunset - Sunrise;

        /// <summary>
        /// The day <paramref name="t"/> of the way from <paramref name="from"/> to <paramref name="to"/> (0 is
        /// <paramref name="from"/>, 1 is <paramref name="to"/>): every time and height blended alike, so it is always a valid day.
        /// </summary>
        public static DayCycle Between(DayCycle from, DayCycle to, float t)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));
            if (to == null)
                throw new ArgumentNullException(nameof(to));

            t = Math.Max(0f, Math.Min(1f, t));
            return new DayCycle(Lerp(from.Sunrise, to.Sunrise, t), Lerp(from.Sunset, to.Sunset, t), Lerp(from.NoonHeight, to.NoonHeight, t));
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
