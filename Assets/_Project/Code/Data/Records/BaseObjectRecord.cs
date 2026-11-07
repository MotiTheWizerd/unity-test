using System;
using System.Collections.Generic;

namespace Forge.Data.Records
{
    /// <summary>
    /// A definition of something that can be placed in the world (a tree, a rock,
    /// a chair). Placed instances reference it by id, so shared data lives once.
    /// What it is beyond its look comes in optional parts: an <see cref="ItemPart"/> makes it something to carry, and an
    /// <see cref="EquippablePart"/> something its carrier can equip.
    /// </summary>
    public sealed class BaseObjectRecord : Record
    {
        /// <param name="category">Where the palette files it ("Nature/Trees"); null or blank = uncategorized.</param>
        /// <param name="placement">How the editor places new instances; null = exactly as chosen, upright.</param>
        /// <param name="materials">Slots every instance wears another material in than its asset does (a variant's look); null or empty = none.</param>
        /// <param name="item">What makes it an item to pick up and carry; null = not an item.</param>
        /// <param name="equippable">What makes it something to equip and hold or wear; null = not equippable. Only an item is.</param>
        public BaseObjectRecord(RecordId id, string displayName, string assetKey, string category = null, PlacementRules placement = null,
            IEnumerable<MaterialOverride> materials = null, ItemPart item = null, EquippablePart equippable = null)
            : base(id)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));
            if (equippable != null && item == null)
                throw new ArgumentException("Only an item is equippable: give it an item part too.", nameof(equippable));

            DisplayName = displayName;
            AssetKey = assetKey;
            Category = CategoryPath.Normalize(category);
            Placement = placement;
            var validated = MaterialOverrides.Validated(materials, nameof(materials));
            Materials = validated.Count > 0 ? validated : null;
            Item = item;
            Equippable = equippable;
        }

        public string DisplayName { get; }

        /// <summary>Engine-agnostic key the presentation layer resolves to a visual asset.</summary>
        public string AssetKey { get; }

        /// <summary>Normalized <see cref="CategoryPath"/>; empty when uncategorized.</summary>
        public string Category { get; }

        /// <summary>Placement rules for new instances, or null: placed exactly as chosen, upright.</summary>
        public PlacementRules Placement { get; }

        /// <summary>
        /// The slots every instance wears another material in than the asset does, sorted by slot (an instance's own
        /// <see cref="PlacedObjectRecord.Materials"/> layer on top), or null: the asset's look. How a variant of an object
        /// differs from it, as in the Creation Kit.
        /// </summary>
        public IReadOnlyList<MaterialOverride> Materials { get; }

        /// <summary>What makes it an item an actor can pick up and carry, or null: not an item.</summary>
        public ItemPart Item { get; }

        /// <summary>What makes it something its carrier can equip, or null: not equippable.</summary>
        public EquippablePart Equippable { get; }
    }
}
