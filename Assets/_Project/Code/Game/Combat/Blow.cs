using System;
using System.Numerics;

namespace Forge.Game.Combat
{
    /// <summary>
    /// A blow as the one it struck feels it (<see cref="IGuard.Jolt"/>): how long it stops it
    /// (<see cref="ActorCombat.ImpactSeconds"/>), which way it travelled, and how much of it reached the body past the
    /// guard, which the struck one gives with (<see cref="ActorCombat.Struck"/>).
    /// </summary>
    public readonly struct Blow
    {
        /// <param name="seconds">How long it stops whatever the struck one is doing, seconds.</param>
        /// <param name="direction">Which way it travelled (scene space); null: from wherever.</param>
        /// <param name="reached">How much of it reached the body, 0 to 1: 1 struck it whole, 0 a guard stopped it all.</param>
        public Blow(float seconds, Vector3? direction, float reached)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Must be 0 or more.");
            if (!(reached >= 0f) || !(reached <= 1f))
                throw new ArgumentOutOfRangeException(nameof(reached), reached, "A share, 0 to 1.");
            Seconds = seconds;
            Direction = direction;
            Reached = reached;
        }

        /// <summary>How long it stops whatever the struck one is doing, seconds.</summary>
        public float Seconds { get; }

        /// <summary>Which way it travelled (scene space); null: from wherever.</summary>
        public Vector3? Direction { get; }

        /// <summary>How much of it reached the body past the guard, 0 to 1.</summary>
        public float Reached { get; }

        /// <summary>Whether any of it reached the body.</summary>
        public bool ReachedBody => Reached > 0f;
    }
}
