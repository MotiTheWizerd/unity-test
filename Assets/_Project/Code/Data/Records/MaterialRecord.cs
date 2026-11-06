using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// A surface a placed object can wear in one of its material slots (wood, stone, plaster), like a Creation Kit
    /// material object. How it looks is the presentation's business, found by <see cref="AssetKey"/>, so a mod restyles
    /// a material everywhere by overriding it, or adds its own.
    /// </summary>
    public sealed class MaterialRecord : Record
    {
        /// <summary>Name prefix of material records, e.g. "forge.core:material/oak_planks".</summary>
        public const string NamePrefix = "material/";

        public MaterialRecord(RecordId id, string displayName, string assetKey)
            : base(id)
        {
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Material ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));

            DisplayName = displayName;
            AssetKey = assetKey;
        }

        public string DisplayName { get; }

        /// <summary>Engine-agnostic key the presentation resolves to the material's look.</summary>
        public string AssetKey { get; }

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);
    }
}
