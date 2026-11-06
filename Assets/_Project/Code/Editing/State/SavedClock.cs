using Forge.Data.Sky;

namespace Forge.Editing.State
{
    /// <summary>The editor's time of day as it is kept between sessions.</summary>
    public sealed class SavedClock
    {
        public int Day { get; set; }
        public float Hour { get; set; }
        public bool Running { get; set; }

        public static SavedClock Of(WorldClock clock) => new SavedClock { Day = clock.Day, Hour = clock.Hour, Running = clock.Running };

        /// <summary>Sets <paramref name="clock"/> to this time; values a clock can't hold (a negative day, a NaN hour) are skipped.</summary>
        public void ApplyTo(WorldClock clock)
        {
            if (Day >= 0)
                clock.Day = Day;
            if (!float.IsNaN(Hour) && !float.IsInfinity(Hour))
                clock.Hour = Hour;
            clock.Running = Running;
        }
    }
}
