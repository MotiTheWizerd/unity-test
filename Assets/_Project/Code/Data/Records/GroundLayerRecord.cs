using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// A kind of ground the land can be painted with (grass, dirt, rock, sand), like a Bethesda landscape texture.
    /// Terrain chunks name the layers they are painted with (<see cref="Terrain.GroundPaint"/>); how a layer looks is
    /// the presentation's business, found by <see cref="AssetKey"/>, so a mod restyles a layer everywhere by overriding
    /// it, or adds its own.
    /// </summary>
    public sealed class GroundLayerRecord : Record
    {
        /// <summary>Name prefix of ground layer records, e.g. "forge.core:ground/grass".</summary>
        public const string NamePrefix = "ground/";

        public GroundLayerRecord(RecordId id, string displayName, string assetKey)
            : base(id)
        {
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Ground layer ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));

            DisplayName = displayName;
            AssetKey = assetKey;
        }

        public string DisplayName { get; }

        /// <summary>Engine-agnostic key the presentation resolves to the layer's look (its textures).</summary>
        public string AssetKey { get; }

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);
    }
}
