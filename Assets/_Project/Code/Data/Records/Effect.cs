using System;

namespace Forge.Data.Records
{
    /// <summary>What an effect does to its attribute.</summary>
    public enum EffectKind
    {
        /// <summary>Takes from a resource (a blow's damage to health).</summary>
        Damage,

        /// <summary>Gives back to a resource (a healing draught).</summary>
        Restore,
    }

    /// <summary>
    /// One thing done to whoever is struck or affected, as a Bethesda magic effect: damage or restore an attribute by an
    /// amount. What a weapon's blow does (<see cref="WeaponPart.Hit"/>) is a list of them, and so will be what a spell or
    /// a potion does, so every way of hurting or healing is the same data. Durations and damage types come as they are needed.
    /// </summary>
    public sealed class Effect
    {
        /// <param name="attribute">The attribute it acts on (a resource: health, stamina, magicka).</param>
        /// <param name="amount">How much, more than zero.</param>
        public Effect(EffectKind kind, RecordId attribute, float amount)
        {
            if (!Enum.IsDefined(typeof(EffectKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown effect kind.");
            if (attribute.IsDefault)
                throw new ArgumentException("An effect names its attribute.", nameof(attribute));
            if (!(amount > 0f) || float.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "An effect's amount must be a finite number above zero.");

            Kind = kind;
            Attribute = attribute;
            Amount = amount;
        }

        public EffectKind Kind { get; }

        public RecordId Attribute { get; }

        public float Amount { get; }

        /// <summary>The same effect, <paramref name="factor"/> (more than zero) times as strong: a bow drawn halfway.</summary>
        public Effect Times(float factor) => factor == 1f ? this : new Effect(Kind, Attribute, Amount * factor);

        public static Effect Damage(RecordId attribute, float amount) => new Effect(EffectKind.Damage, attribute, amount);

        public static Effect Restore(RecordId attribute, float amount) => new Effect(EffectKind.Restore, attribute, amount);
    }
}
