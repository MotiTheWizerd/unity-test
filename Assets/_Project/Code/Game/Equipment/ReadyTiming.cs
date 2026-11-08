using System;

namespace Forge.Game.Equipment
{
    /// <summary>
    /// How a draw or a sheathe plays: how long it lasts, and how far into it the weapon changes place (from where it is
    /// carried to the hand, or back). The look's to say, measured from the move's clip; the rules only count time.
    /// </summary>
    public readonly struct ReadyTiming
    {
        /// <param name="seconds">How long it lasts; zero or more (zero is at once).</param>
        /// <param name="at">How far into it the weapon changes place, 0 to 1.</param>
        public ReadyTiming(float seconds, float at)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Must be a finite number of seconds, zero or more.");
            if (!(at >= 0f && at <= 1f))
                throw new ArgumentOutOfRangeException(nameof(at), at, "Must be a fraction of the move, 0 to 1.");
            Seconds = seconds;
            At = at;
        }

        public float Seconds { get; }

        public float At { get; }
    }

    /// <summary>The look's timings of an actor's draws and sheathes, by the animation that plays them (a style's move: "BowDraw").</summary>
    public interface IReadyTimings
    {
        /// <summary>How <paramref name="animation"/> plays; false when the look has no such move.</summary>
        bool TryGet(string animation, out ReadyTiming timing);
    }
}
