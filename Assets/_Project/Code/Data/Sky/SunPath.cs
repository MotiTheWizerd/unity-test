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
        public static SkyPosition At(DayCycle day, float hour) => Arc(day.Sunrise, day.Sunset, day.NoonHeight, hour);

        /// <summary>
        /// The arc itself, for anything that crosses the sky the sun's way (the moons do, with their own height and a
        /// delay): up at <paramref name="rise"/> in the east, <paramref name="highest"/> degrees up in the south
        /// halfway to <paramref name="set"/> in the west, under the horizon through the north in between.
        /// </summary>
        public static SkyPosition Arc(float rise, float set, float highest, float hour)
        {
            var time = WorldClock.Wrap(hour);
            var upLength = set - rise;
            var downLength = WorldClock.HoursPerDay - upLength;

            if (time >= rise && time <= set)
            {
                var progress = (time - rise) / upLength;
                return new SkyPosition(highest * (float)Math.Sin(Math.PI * progress), 90f + 180f * progress);
            }

            // Hours since setting, across midnight.
            var sinceSet = time > set ? time - set : time + WorldClock.HoursPerDay - set;
            var down = sinceSet / downLength;
            return new SkyPosition(-NightDepth * (float)Math.Sin(Math.PI * down), (270f + 180f * down) % 360f);
        }
    }
}
