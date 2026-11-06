using System;

namespace Forge.Game.Actors
{
    /// <summary>
    /// Who an actor is, so what happens can say to whom (<see cref="Forge.Game.Events.GameEvents"/>): the player, and the
    /// world's characters later (a placed actor by its record's id). Compared by key; cheap to copy.
    /// </summary>
    public readonly struct ActorId : IEquatable<ActorId>
    {
        /// <summary>The one playing.</summary>
        public static readonly ActorId Player = new ActorId("player");

        public ActorId(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("An actor id has a key.", nameof(key));
            Key = key;
        }

        public string Key { get; }

        /// <summary>Whether this is no actor at all (the default value).</summary>
        public bool IsDefault => Key == null;

        public bool Equals(ActorId other) => string.Equals(Key, other.Key, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ActorId other && Equals(other);

        public override int GetHashCode() => Key == null ? 0 : StringComparer.Ordinal.GetHashCode(Key);

        public static bool operator ==(ActorId a, ActorId b) => a.Equals(b);

        public static bool operator !=(ActorId a, ActorId b) => !a.Equals(b);

        public override string ToString() => Key ?? "(no actor)";
    }
}
