using System;
using Forge.Data;

namespace Forge.Game.Attributes
{
    /// <summary>How a modifier changes an attribute; see <see cref="ActorAttributes"/> for the order they apply in.</summary>
    public enum ModifierOp
    {
        /// <summary>Adds the amount (+20 carry weight).</summary>
        Add,

        /// <summary>Adds the amount in percent of the added-up value (+25 is a quarter more); percents add together.</summary>
        Percent,

        /// <summary>Multiplies by the amount (0.5 halves); multipliers multiply together.</summary>
        Multiply,
    }

    /// <summary>One change to one attribute, as an item, an effect or a perk makes it.</summary>
    public readonly struct AttributeModifier
    {
        public AttributeModifier(RecordId attribute, ModifierOp op, float amount)
        {
            if (attribute.IsDefault)
                throw new ArgumentException("A modifier names the attribute it changes.", nameof(attribute));
            if (!Enum.IsDefined(typeof(ModifierOp), op))
                throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown modifier op.");
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must be a finite number.");
            Attribute = attribute;
            Op = op;
            Amount = amount;
        }

        public RecordId Attribute { get; }

        public ModifierOp Op { get; }

        public float Amount { get; }

        public static AttributeModifier Add(RecordId attribute, float amount) => new AttributeModifier(attribute, ModifierOp.Add, amount);

        public static AttributeModifier Percent(RecordId attribute, float percent) => new AttributeModifier(attribute, ModifierOp.Percent, percent);

        public static AttributeModifier Multiply(RecordId attribute, float factor) => new AttributeModifier(attribute, ModifierOp.Multiply, factor);
    }

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
