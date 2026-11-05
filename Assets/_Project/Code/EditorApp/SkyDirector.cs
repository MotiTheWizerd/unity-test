using System;
using Forge.Data.Records;
using Forge.World.Sky;

namespace Forge.EditorApp
{
    /// <summary>
    /// Keeps the sky on the editor's clock: lets time pass while the clock runs and re-renders the sky
    /// (<see cref="EditorContext.Sky"/>: the sun, the moons, the stars' turn) once enough time has passed for anything in
    /// it to move visibly, or the climate it follows changed (another space shown, a climate picked or edited), so a
    /// running clock redraws it every few seconds and the time slider and day field redraw it as they change.
    /// </summary>
    public sealed class SkyDirector
    {
        /// <summary>
        /// Game hours between redraws: half a game minute, in which nothing in the sky moves more than about 0.2° (the
        /// sun is quickest, crossing the night's half circle in 10 hours).
        /// </summary>
        const float VisibleStep = 0.5f / 60f;

        readonly SkyView _sky;
        readonly EditorContext _context;
        (int Day, float Hour, ClimateRecord Climate)? _shown;

        public SkyDirector(SkyView sky, EditorContext context)
        {
            _sky = sky ?? throw new ArgumentNullException(nameof(sky));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void Tick(float realSeconds)
        {
            var clock = _context.Clock;
            clock.Advance(realSeconds);
            // Records are immutable, so a climate that changed is a different record.
            var climate = _context.ActiveClimate;
            if (_shown is (int day, float hour, var shownClimate)
                && day == clock.Day && Math.Abs(clock.Hour - hour) < VisibleStep && ReferenceEquals(climate, shownClimate))
                return;

            _sky.Show(_context.Sky);
            _shown = (clock.Day, clock.Hour, climate);
        }
    }
}
