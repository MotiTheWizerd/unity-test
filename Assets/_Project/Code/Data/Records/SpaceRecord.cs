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
        /// <param name="waterLevel">Height of the sea's surface in a worldspace, in meters, or null for no sea; ignored for interiors.</param>
        /// <param name="climate">The <see cref="ClimateRecord"/> a worldspace's sky follows, or null for none; ignored for interiors.</param>
        /// <param name="heightmapResolution">Samples per edge of each terrain chunk of a worldspace (65 for 1 m between samples, 33 for 2 m), or null for the default; ignored for interiors. Nullable so a file without it (an older plugin) reads as the default: the serializer passes 0, not the C# default, for a missing argument.</param>
        public SpaceRecord(RecordId id, string displayName, SpaceKind kind, float baseHeight = 0f, float? waterLevel = null, RecordId? climate = null,
            int? heightmapResolution = null)
            : base(id)
        {
            var resolution = heightmapResolution ?? WorldGrid.DefaultHeightmapResolution;
            if (!id.Name.StartsWith(NamePrefix, StringComparison.Ordinal))
                throw new ArgumentException($"Space ids are named '{NamePrefix}…', not '{id.Name}'.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (!Enum.IsDefined(typeof(SpaceKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown space kind.");
            if (!IsTerrainHeight(baseHeight))
                throw new ArgumentOutOfRangeException(nameof(baseHeight), baseHeight, $"Base height must be within [0, {WorldGrid.MaxTerrainHeight}] m.");
            if (waterLevel.HasValue && !IsTerrainHeight(waterLevel.Value))
                throw new ArgumentOutOfRangeException(nameof(waterLevel), waterLevel, $"Water level must be within [0, {WorldGrid.MaxTerrainHeight}] m.");
            if (climate.HasValue && climate.Value.IsDefault)
                throw new ArgumentException("A climate id must be set; leave it out for none.", nameof(climate));
            if (!WorldGrid.IsValidHeightmapResolution(resolution))
                throw new ArgumentOutOfRangeException(nameof(heightmapResolution), resolution, "Heightmap resolution must be 2^n + 1 within the supported range (e.g. 65 or 33).");

            DisplayName = displayName;
            Kind = kind;
            BaseHeight = kind == SpaceKind.Worldspace ? baseHeight : 0f;
            WaterLevel = kind == SpaceKind.Worldspace ? waterLevel : null;
            Climate = kind == SpaceKind.Worldspace ? climate : null;
            HeightmapResolution = kind == SpaceKind.Worldspace ? resolution : WorldGrid.DefaultHeightmapResolution;
        }

        public string DisplayName { get; }

        public SpaceKind Kind { get; }

        /// <summary>Height new land is created at, in meters (0 for interiors, which have no land).</summary>
        public float BaseHeight { get; }

        /// <summary>
        /// Height of the sea's surface in meters, or null for no sea (always null for interiors). Land below it is
        /// under water, whether it is a coast, a bay or a hollow inland.
        /// </summary>
        public float? WaterLevel { get; }

        /// <summary>
        /// The <see cref="ClimateRecord"/> the sky follows here (day length through the seasons, the moons), or null
        /// for none: then every day is <see cref="Sky.DayCycle.Default"/> under the default moons. Always null for
        /// interiors, which have no sky of their own.
        /// </summary>
        public RecordId? Climate { get; }

        /// <summary>
        /// Samples per edge of each of this worldspace's terrain chunks: the land's detail, chosen when the space is
        /// created (new chunks are made at it, and every chunk of a space shares it). 65 is a sample every meter,
        /// 33 every 2 m: a quarter of the data for open land. See <see cref="SampleSpacing"/>.
        /// </summary>
        public int HeightmapResolution { get; }

        /// <summary>Meters between the terrain's height samples (1 m at the default resolution).</summary>
        public float SampleSpacing => WorldGrid.SpacingOf(HeightmapResolution);

        public SpaceRecord WithWaterLevel(float? waterLevel) => new SpaceRecord(Id, DisplayName, Kind, BaseHeight, waterLevel, Climate, HeightmapResolution);

        public SpaceRecord WithClimate(RecordId? climate) => new SpaceRecord(Id, DisplayName, Kind, BaseHeight, WaterLevel, climate, HeightmapResolution);

        public static RecordId IdFor(string ns, string name) => new RecordId(ns, NamePrefix + name);

        static bool IsTerrainHeight(float height) => height >= 0f && height <= WorldGrid.MaxTerrainHeight;
    }
}
