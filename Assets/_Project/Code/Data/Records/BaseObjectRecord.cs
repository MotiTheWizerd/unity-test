using System;
using System.Collections.Generic;

namespace Forge.Data.Records
{
    /// <summary>
    /// A definition of something that can be placed in the world (a tree, a rock,
    /// a chair). Placed instances reference it by id, so shared data lives once.
    /// </summary>
    public sealed class BaseObjectRecord : Record
    {
        /// <param name="category">Where the palette files it ("Nature/Trees"); null or blank = uncategorized.</param>
        /// <param name="placement">How the editor places new instances; null = exactly as chosen, upright.</param>
        /// <param name="materials">Slots every instance wears another material in than its asset does (a variant's look); null or empty = none.</param>
        public BaseObjectRecord(RecordId id, string displayName, string assetKey, string category = null, PlacementRules placement = null,
            IEnumerable<MaterialOverride> materials = null)
            : base(id)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));

            DisplayName = displayName;
            AssetKey = assetKey;
            Category = CategoryPath.Normalize(category);
            Placement = placement;
            var validated = MaterialOverrides.Validated(materials, nameof(materials));
            Materials = validated.Count > 0 ? validated : null;
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
    }
}
