using System;
using Forge.Data.Terrain;

namespace Forge.Data.Records
{
    public enum SpaceKind
    {
        /// <summary>Open land: terrain chunks, sky, as large as its land.</summary>
        Worldspace,

        /// <summary>An inside (a house, a cave): no terrain, floors and walls are objects.</summary>
        Interior,
    }

    /// <summary>
    /// A separate space of the world, like a Bethesda worldspace or interior cell: the main land, a walled city,
    /// a house. Terrain chunks and placed objects belong to exactly one space, and positions are local to it.
    /// A worldspace's extent is its terrain chunks, so it has no size of its own: land is added as chunks.
    /// </summary>
    public sealed class SpaceRecord : Record
    {
        /// <summary>Name prefix of space records, e.g. "forge.core:space/main".</summary>
        public const string NamePrefix = "space/";

        /// <param name="baseHeight">Height of new land in a worldspace, in meters; ignored for interiors.</param>
        public SpaceRecord(RecordId id, string displayName, SpaceKind kind, float baseHeight = 0f)
            : base(id)
        {
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Space ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (!Enum.IsDefined(typeof(SpaceKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown space kind.");
            if (!(baseHeight >= 0f && baseHeight <= WorldGrid.MaxTerrainHeight))
                throw new ArgumentOutOfRangeException(nameof(baseHeight), baseHeight, $"Base height must be within [0, {WorldGrid.MaxTerrainHeight}] m.");

            DisplayName = displayName;
            Kind = kind;
            BaseHeight = kind == SpaceKind.Worldspace ? baseHeight : 0f;
        }

        public string DisplayName { get; }

        public SpaceKind Kind { get; }

        /// <summary>Height new land is created at, in meters (0 for interiors, which have no land).</summary>
        public float BaseHeight { get; }

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);
    }
}
