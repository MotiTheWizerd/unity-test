using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// A definition of something that can be placed in the world (a tree, a rock,
    /// a chair). Placed instances reference it by id, so shared data lives once.
    /// </summary>
    public sealed class BaseObjectRecord : Record
    {
        /// <param name="category">Where the palette files it ("Nature/Trees"); null or blank = uncategorized.</param>
        /// <param name="variation">How new instances may vary when placed; null = placed exactly as chosen.</param>
        public BaseObjectRecord(RecordId id, string displayName, string assetKey, string category = null, PlacementVariation variation = null)
            : base(id)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));

            DisplayName = displayName;
            AssetKey = assetKey;
            Category = CategoryPath.Normalize(category);
            Variation = variation;
        }

        public string DisplayName { get; }

        /// <summary>Engine-agnostic key the presentation layer resolves to a visual asset.</summary>
        public string AssetKey { get; }

        /// <summary>Normalized <see cref="CategoryPath"/>; empty when uncategorized.</summary>
        public string Category { get; }

        /// <summary>Random variation for new instances, or null for none.</summary>
        public PlacementVariation Variation { get; }
    }
}
