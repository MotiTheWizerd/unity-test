using System;
using Forge.Data.Sky;
using Forge.World.Sky;

namespace Forge.EditorApp
{
    /// <summary>
    /// Keeps the sky on the clock: lets time pass while the clock runs, works out where the sun stands
    /// (<see cref="SunPath"/>) and re-renders the sky once the sun has moved visibly, so a running clock redraws it
    /// every few seconds and the time slider redraws it as it is dragged.
    /// </summary>
    public sealed class SkyDirector
    {
        /// <summary>How far the sun moves, in degrees, before the sky is drawn again.</summary>
        const float VisibleMove = 0.2f;

        readonly SkyView _sky;
        readonly WorldClock _clock;
        readonly DayCycle _day;
        SunPosition? _shown;

        public SkyDirector(SkyView sky, WorldClock clock, DayCycle day)
        {
            _sky = sky ?? throw new ArgumentNullException(nameof(sky));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _day = day;
        }

        public void Tick(float realSeconds)
        {
            _clock.Advance(realSeconds);
            var sun = SunPath.At(_day, _clock.Hour);
            if (_shown is SunPosition shown && Math.Abs(sun.Height - shown.Height) < VisibleMove && AngleBetween(sun.Bearing, shown.Bearing) < VisibleMove)
                return;

            _sky.Show(sun);
            _shown = sun;
        }

        static float AngleBetween(float a, float b) => Math.Abs(((a - b) % 360f + 540f) % 360f - 180f);
    }
}
