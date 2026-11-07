using System;

namespace Forge.Game.Combat
{
    /// <summary>
    /// How a combat move plays: how long it lasts and the part of it in which a blow can land (its hit window, fractions
    /// of the move). The look's to say, measured from the move's clip, as a draw's length is; the rules only count time.
    /// </summary>
    public readonly struct MoveTiming
    {
        /// <summary>A move the look can't time: a second long, landing in its middle third.</summary>
        public static readonly MoveTiming Default = new MoveTiming(1f, 1f / 3f, 2f / 3f);

        /// <param name="seconds">How long it lasts; more than zero.</param>
        /// <param name="hitFrom">Where its hit window opens, 0 to 1.</param>
        /// <param name="hitTo">Where its hit window closes, from <paramref name="hitFrom"/> to 1.</param>
        public MoveTiming(float seconds, float hitFrom, float hitTo)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "A move lasts a finite number of seconds, more than zero.");
            if (!(hitFrom >= 0f && hitFrom <= 1f))
                throw new ArgumentOutOfRangeException(nameof(hitFrom), hitFrom, "Must be a fraction of the move, 0 to 1.");
            if (!(hitTo >= hitFrom && hitTo <= 1f))
                throw new ArgumentOutOfRangeException(nameof(hitTo), hitTo, "Must be a fraction of the move, from the window's opening to 1.");

            Seconds = seconds;
            HitFrom = hitFrom;
            HitTo = hitTo;
        }

        public float Seconds { get; }

        public float HitFrom { get; }

        public float HitTo { get; }

        public override string ToString() => $"{Seconds:0.##} s, hits {HitFrom:0.##}–{HitTo:0.##}";
    }

    /// <summary>The look's timings of an actor's moves, by the animation that plays them (<see cref="Data.Records.CombatMove.Animation"/>).</summary>
    public interface IMoveTimings
    {
        /// <summary>How <paramref name="animation"/> plays; false when the look has no such move.</summary>
        bool TryGet(string animation, out MoveTiming timing);
    }
}
