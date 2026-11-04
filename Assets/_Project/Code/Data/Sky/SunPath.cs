using System;

namespace Forge.Data.Sky
{
    /// <summary>
    /// Where the sun stands at an hour of a <see cref="DayCycle"/>: a stylized arc, not astronomy. By day it rises in
    /// the east (bearing 90), stands highest in the south at the middle of the day (180) and sets in the west (270);
    /// by night it carries on under the horizon through the north, as deep below it at midnight as
    /// <see cref="NightDepth"/>. Height and bearing are continuous through sunrise and sunset, so the sky never jumps.
    /// </summary>
    public static class SunPath
    {
        /// <summary>How far below the horizon the sun sinks in the middle of the night, in degrees.</summary>
        public const float NightDepth = 40f;

        /// <param name="hour">Time of day in hours, wrapped into [0, 24).</param>
        public static SunPosition At(DayCycle day, float hour)
        {
            var time = WorldClock.Wrap(hour);
            var dayLength = day.Sunset - day.Sunrise;
            var nightLength = WorldClock.HoursPerDay - dayLength;

            if (time >= day.Sunrise && time <= day.Sunset)
            {
                var progress = (time - day.Sunrise) / dayLength;
                return new SunPosition(day.NoonHeight * (float)Math.Sin(Math.PI * progress), 90f + 180f * progress);
            }

            // Hours since sunset, across midnight.
            var sinceSunset = time > day.Sunset ? time - day.Sunset : time + WorldClock.HoursPerDay - day.Sunset;
            var night = sinceSunset / nightLength;
            return new SunPosition(-NightDepth * (float)Math.Sin(Math.PI * night), (270f + 180f * night) % 360f);
        }
    }
}
