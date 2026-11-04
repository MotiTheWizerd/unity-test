using System;

namespace Forge.Data.Sky
{
    /// <summary>
    /// The world's time: the day number and the hour, running at <see cref="Scale"/> game seconds per real second
    /// (Skyrim's 20: a day lasts 72 real minutes) while <see cref="Running"/>. The one source of time for the sky
    /// and, later, for shops, window lights and NPC schedules. In the editor it is a view setting, never saved; the
    /// game will save it with the player's progress.
    /// </summary>
    public sealed class WorldClock
    {
        public const float HoursPerDay = 24f;
        public const float DefaultScale = 20f;

        float _hour;
        float _scale = DefaultScale;

        /// <param name="hour">The time of day to start at, in hours.</param>
        public WorldClock(float hour = 10f)
        {
            Hour = hour;
        }

        /// <summary>Time of day in hours, [0, 24).</summary>
        public float Hour
        {
            get => _hour;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The hour must be a finite number.");
                _hour = Wrap(value);
            }
        }

        /// <summary>Days passed since the clock started.</summary>
        public int Day { get; private set; }

        /// <summary>Game seconds per real second.</summary>
        public float Scale
        {
            get => _scale;
            set => _scale = value > 0f && !float.IsInfinity(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The time scale must be positive.");
        }

        /// <summary>Whether time passes on its own (<see cref="Advance"/>); otherwise it only moves when set.</summary>
        public bool Running { get; set; }

        /// <summary>Lets <paramref name="realSeconds"/> pass, if the clock is running; past midnight a new day starts.</summary>
        public void Advance(float realSeconds)
        {
            if (!Running || !(realSeconds > 0f))
                return;

            var hour = _hour + realSeconds * _scale / 3600f;
            var days = (int)Math.Floor(hour / HoursPerDay);
            Day += days;
            _hour = hour - days * HoursPerDay;
        }

        /// <summary>The hour as "HH:MM".</summary>
        public static string Format(float hour)
        {
            var minutes = (int)Math.Floor(Wrap(hour) * 60f + 1e-4f) % (int)(HoursPerDay * 60f);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        /// <summary><paramref name="hour"/> wrapped into [0, 24).</summary>
        public static float Wrap(float hour)
        {
            var wrapped = hour % HoursPerDay;
            if (wrapped < 0f)
                wrapped += HoursPerDay;
            return wrapped >= HoursPerDay ? 0f : wrapped;
        }
    }
}
