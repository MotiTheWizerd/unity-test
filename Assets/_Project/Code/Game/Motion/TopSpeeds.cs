using System;
using System.Numerics;

namespace Forge.Game.Motion
{
    /// <summary>
    /// How fast a body goes at most, as fast as its look can show it going, by the way it goes as the body has it:
    /// <see cref="Ahead"/> forward or back (a run, played backward to back away), <see cref="Aside"/> to either side (a
    /// sidestep), and between them as far as the two allow together (<see cref="Along"/>: on the ellipse through both),
    /// as a diagonal blends a step ahead with one aside. Meters per second; <see cref="None"/>, as the default is, holds
    /// nothing back.
    /// </summary>
    public readonly struct TopSpeeds
    {
        // 0 (the default's) is no limit: a limit is more than 0.
        readonly float _ahead;
        readonly float _aside;

        /// <param name="ahead">Meters per second at most going forward or back; infinity for no limit.</param>
        /// <param name="aside">Meters per second at most going to either side; infinity for no limit.</param>
        public TopSpeeds(float ahead, float aside)
        {
            _ahead = Positive(ahead, nameof(ahead));
            _aside = Positive(aside, nameof(aside));
        }

        /// <summary>No limit, whichever way it goes.</summary>
        public static TopSpeeds None => default;

        /// <summary>Meters per second at most going forward or back; infinity for none.</summary>
        public float Ahead => _ahead > 0f ? _ahead : float.PositiveInfinity;

        /// <summary>Meters per second at most going to either side; infinity for none.</summary>
        public float Aside => _aside > 0f ? _aside : float.PositiveInfinity;

        /// <summary>
        /// Meters per second at most going <paramref name="way"/> as the body has it (x to its right, y ahead; its length
        /// doesn't matter): <see cref="Ahead"/> straight ahead or back, <see cref="Aside"/> straight to a side, on the
        /// ellipse through both between them; straight ahead's for no way at all.
        /// </summary>
        public float Along(Vector2 way)
        {
            var length = way.Length();
            if (!(length > 1e-6f))
                return Ahead;
            // Going 1 m/s that way, how much of each limit it takes: the top speed is where the two make one together.
            var aside = way.X / length / Aside;
            var ahead = way.Y / length / Ahead;
            var taken = aside * aside + ahead * ahead;
            return taken > 0f ? 1f / (float)Math.Sqrt(taken) : float.PositiveInfinity;
        }

        static float Positive(float value, string name) =>
            value > 0f ? value : throw new ArgumentOutOfRangeException(name, value, "A top speed is more than 0 meters a second.");
    }
}
