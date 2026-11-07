using System;

namespace Forge.Game.Attributes
{
    /// <summary>
    /// Where modifiers came from: an item worn, an effect, a perk. Whoever adds modifiers names their source, and taking
    /// the source away (<see cref="ActorAttributes.Remove"/>) takes exactly its modifiers with it. Compared by key.
    /// </summary>
    public readonly struct ModifierSource : IEquatable<ModifierSource>
    {
        public ModifierSource(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A source has a key.", nameof(key));
            Key = key;
        }

        public string Key { get; }

        public bool Equals(ModifierSource other) => string.Equals(Key, other.Key, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ModifierSource other && Equals(other);

        public override int GetHashCode() => Key == null ? 0 : StringComparer.Ordinal.GetHashCode(Key);

        public static bool operator ==(ModifierSource a, ModifierSource b) => a.Equals(b);

        public static bool operator !=(ModifierSource a, ModifierSource b) => !a.Equals(b);

        public override string ToString() => Key;
    }
}
