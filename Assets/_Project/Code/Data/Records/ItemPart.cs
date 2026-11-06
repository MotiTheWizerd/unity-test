using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// What makes a base object an item: something an actor can pick up and carry (a sword, a potion, gold), like the
    /// item forms of a Bethesda game. A part, not a kind of record: any <see cref="BaseObjectRecord"/> with one is an item,
    /// so items are placed, dressed and varied like every other object, and a mod makes anything takeable by giving it
    /// one. What using an item does (wielding, wearing, drinking) will be parts of their own.
    /// </summary>
    public sealed class ItemPart
    {
        /// <param name="kind">Where an inventory lists it.</param>
        /// <param name="weight">What one weighs, against an actor's carry weight; zero or more.</param>
        /// <param name="value">What one is worth, in gold; zero or more.</param>
        public ItemPart(ItemKind kind, float weight, int value)
        {
            if (!Enum.IsDefined(typeof(ItemKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown item kind.");
            if (!(weight >= 0f) || float.IsInfinity(weight))
                throw new ArgumentOutOfRangeException(nameof(weight), weight, "An item's weight must be a finite number, zero or more.");
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "An item's value can't be negative.");

            Kind = kind;
            Weight = weight;
            Value = value;
        }

        public ItemKind Kind { get; }

        public float Weight { get; }

        public int Value { get; }

        // No derived properties here: serializers write every public property, and a plugin file holds data only.
    }
}
