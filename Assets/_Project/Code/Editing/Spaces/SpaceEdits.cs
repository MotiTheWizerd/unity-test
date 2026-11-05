using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Spaces
{
    /// <summary>Creates spaces in the active plugin and changes their settings.</summary>
    public static class SpaceEdits
    {
        /// <summary>Land sizes offered for a new worldspace, in chunks along each edge.</summary>
        public static readonly IReadOnlyList<int> Sizes = new[] { 3, 8, 16 };

        public const int MaxSize = 16;

        /// <summary>
        /// Creates a worldspace of <paramref name="size"/> × <paramref name="size"/> chunks of flat land at
        /// <paramref name="baseHeight"/>, centered on the origin, as one undo step. Returns the new space's id.
        /// </summary>
        public static RecordId CreateWorldspace(EditSession session, string displayName, int size, float baseHeight)
        {
            if (!(size >= 1 && size <= MaxSize))
                throw new ArgumentOutOfRangeException(nameof(size), size, $"Size must be within [1, {MaxSize}] chunks.");

            return Create(session, displayName, SpaceKind.Worldspace, baseHeight, space =>
            {
                var heights = HeightField.Flat(baseHeight);
                var first = -size / 2;
                return Enumerable.Range(first, size)
                    .SelectMany(z => Enumerable.Range(first, size).Select(x => new ChunkCoord(x, z)))
                    .Select(coord => new TerrainChunkRecord(TerrainChunkRecord.IdFor(session.ActivePluginId, space, coord), space, coord, heights));
            });
        }

        /// <summary>Creates an empty interior as one undo step. Returns the new space's id.</summary>
        public static RecordId CreateInterior(EditSession session, string displayName) =>
            Create(session, displayName, SpaceKind.Interior, 0f, _ => Enumerable.Empty<Record>());

        /// <summary>
        /// Sets the sea of worldspace <paramref name="space"/> to <paramref name="level"/> meters, or removes it (null),
        /// as one undo step; a master's space is overridden in the active plugin. Returns false when it already is.
        /// </summary>
        public static bool SetWaterLevel(EditSession session, RecordId space, float? level)
        {
            var spaceRecord = Worldspace(session, space, "have a sea");
            if (spaceRecord.WaterLevel == level)
                return false;

            var changed = spaceRecord.WithWaterLevel(level);
            var description = level.HasValue
                ? FormattableString.Invariant($"Sea level of {spaceRecord.DisplayName}: {level.Value:0.##} m")
                : $"Remove the sea of {spaceRecord.DisplayName}";
            session.Batch(description, () => session.Put(changed));
            return true;
        }

        /// <summary>
        /// Sets the climate of worldspace <paramref name="space"/> to <paramref name="climate"/>, or removes it (null),
        /// as one undo step; a master's space is overridden in the active plugin. Returns false when it already is.
        /// </summary>
        public static bool SetClimate(EditSession session, RecordId space, RecordId? climate)
        {
            var spaceRecord = Worldspace(session, space, "have a climate");
            var climateRecord = default(ClimateRecord);
            if (climate.HasValue && !(session.World.Records.TryGet(climate.Value, out var record) && (climateRecord = record as ClimateRecord) != null))
                throw new ArgumentException($"There is no climate '{climate.Value}'.", nameof(climate));
            if (spaceRecord.Climate == climate)
                return false;

            var changed = spaceRecord.WithClimate(climate);
            var description = climateRecord != null
                ? $"Climate of {spaceRecord.DisplayName}: {climateRecord.DisplayName}"
                : $"Remove the climate of {spaceRecord.DisplayName}";
            session.Batch(description, () => session.Put(changed));
            return true;
        }

        // The worldspace with that id; an interior is refused, since only worldspaces have what is being set.
        static SpaceRecord Worldspace(EditSession session, RecordId space, string what)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (!(session.World.Records.TryGet(space, out var record) && record is SpaceRecord spaceRecord))
                throw new ArgumentException($"There is no space '{space}'.", nameof(space));
            if (spaceRecord.Kind != SpaceKind.Worldspace)
                throw new ArgumentException($"{spaceRecord.DisplayName} is an interior; only worldspaces {what}.", nameof(space));
            return spaceRecord;
        }

        static RecordId Create(EditSession session, string displayName, SpaceKind kind, float baseHeight, Func<RecordId, IEnumerable<Record>> content)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A space needs a name.", nameof(displayName));

            var name = displayName.Trim();
            var id = FreeId(session, name);
            var space = new SpaceRecord(id, name, kind, baseHeight);
            var records = content(id).ToList();
            session.Batch($"New {(kind == SpaceKind.Interior ? "interior" : "worldspace")} {name}", () =>
            {
                session.Put(space);
                foreach (var record in records)
                    session.Put(record);
            });
            return id;
        }

        // "Whiterun Hall" → "space/whiterun_hall", with _2, _3 … when the name is taken.
        static RecordId FreeId(EditSession session, string displayName)
        {
            var slug = Slug(displayName);
            var id = SpaceRecord.IdFor(session.ActivePluginId, slug);
            for (var n = 2; session.World.Records.TryGet(id, out _); n++)
                id = SpaceRecord.IdFor(session.ActivePluginId, $"{slug}_{n}");
            return id;
        }

        /// <summary>A record-name segment for <paramref name="displayName"/>: lowercase letters and digits joined by underscores.</summary>
        public static string Slug(string displayName)
        {
            var slug = new StringBuilder();
            var gap = false;
            foreach (var c in (displayName ?? string.Empty).ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    if (gap && slug.Length > 0)
                        slug.Append('_');
                    slug.Append(c);
                    gap = false;
                }
                else
                {
                    gap = true;
                }
            }

            return slug.Length > 0 ? slug.ToString() : "space";
        }
    }
}
