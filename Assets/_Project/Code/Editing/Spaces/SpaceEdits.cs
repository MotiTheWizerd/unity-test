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
        public static readonly IReadOnlyList<int> Sizes = new[] { 3, 8, 16, 32, 64 };

        /// <summary>Largest land a new worldspace starts with, in chunks along each edge (64 × 64 m ≈ 4 km).</summary>
        public const int MaxSize = 64;

        /// <summary>
        /// Farthest the land may reach on either axis after extending, in chunks (320 × 64 m ≈ 20 km). A sanity
        /// bound, not a design limit: land is its chunks, so raising it costs nothing in the data.
        /// </summary>
        public const int MaxExtent = 320;

        /// <summary>Most chunks one extension adds on a side.</summary>
        public const int MaxExtendBy = 64;

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

        /// <summary>
        /// Grows the land of worldspace <paramref name="space"/> by <paramref name="chunks"/> rows of chunks beyond its
        /// <paramref name="side"/>, the full reach of the land along that side, as one undo step. New land lies at the
        /// space's base height and, where it touches existing land, ramps from that land's edge so the join is
        /// seamless. Gaps inside the land's reach are left as they are. Returns how many chunks were added (chunks
        /// already there are kept).
        /// </summary>
        public static int ExtendLand(EditSession session, RecordId space, LandSide side, int chunks)
        {
            var spaceRecord = Worldspace(session, space, "have land");
            if (!(chunks >= 1 && chunks <= MaxExtendBy))
                throw new ArgumentOutOfRangeException(nameof(chunks), chunks, $"Extend by 1 to {MaxExtendBy} chunks at a time.");

            var existing = session.World.Records.OfType<TerrainChunkRecord>().Where(c => c.Space == space).ToDictionary(c => c.Coord);
            var bounds = LandBounds.Of(existing.Keys)
                         ?? throw new ArgumentException($"{spaceRecord.DisplayName} has no land to extend; create some first.", nameof(space));
            var reach = Math.Max(bounds.Width, bounds.Depth) + chunks;
            if (reach > MaxExtent)
                throw new ArgumentOutOfRangeException(nameof(chunks), chunks, $"The land would reach {reach} chunks across; at most {MaxExtent} ({MaxExtent * WorldGrid.ChunkSize / 1000f:0.#} km) is supported.");

            var outward = side.Outward();
            var added = new List<TerrainChunkRecord>();
            foreach (var edge in EdgeChunks(bounds, side))
            {
                // The ramp starts from the land actually at the edge (it may stop short of the rectangle's corner).
                existing.TryGetValue(edge, out var neighbour);
                for (var k = 1; k <= chunks; k++)
                {
                    var coord = new ChunkCoord(edge.X + outward.X * k, edge.Z + outward.Z * k);
                    if (existing.ContainsKey(coord))
                        continue;
                    var heights = k == 1 && neighbour != null
                        ? Ramp(neighbour.Heights.Load(), side, spaceRecord.BaseHeight)
                        : HeightField.Flat(spaceRecord.BaseHeight, neighbour?.Heights.Resolution ?? WorldGrid.DefaultHeightmapResolution);
                    added.Add(new TerrainChunkRecord(TerrainChunkRecord.IdFor(session.ActivePluginId, space, coord), space, coord, heights));
                }
            }

            if (added.Count == 0)
                return 0;

            session.Batch($"Extend {spaceRecord.DisplayName} {chunks} chunk{(chunks == 1 ? "" : "s")} {side.Word()}", () =>
            {
                foreach (var record in added)
                    session.Put(record);
            });
            return added.Count;
        }

        // The chunk coordinates along the land's edge on that side, one per column (or row) of the land's reach.
        static IEnumerable<ChunkCoord> EdgeChunks(LandBounds bounds, LandSide side)
        {
            switch (side)
            {
                case LandSide.North: return Enumerable.Range(bounds.MinX, bounds.Width).Select(x => new ChunkCoord(x, bounds.MaxZ));
                case LandSide.South: return Enumerable.Range(bounds.MinX, bounds.Width).Select(x => new ChunkCoord(x, bounds.MinZ));
                case LandSide.East: return Enumerable.Range(bounds.MinZ, bounds.Depth).Select(z => new ChunkCoord(bounds.MaxX, z));
                case LandSide.West: return Enumerable.Range(bounds.MinZ, bounds.Depth).Select(z => new ChunkCoord(bounds.MinX, z));
                default: throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        // A chunk beyond `side` of `neighbour`: its shared edge copies the neighbour's edge samples (so the two meet
        // exactly) and the heights ease to `baseHeight` across the chunk.
        static HeightField Ramp(HeightField neighbour, LandSide side, float baseHeight)
        {
            var n = neighbour.Resolution;
            var last = n - 1;
            switch (side)
            {
                case LandSide.North: // shares the neighbour's top row (z = last) as its bottom row (z = 0)
                    return HeightField.FromFunction((x, z) => Lerp(neighbour[x, last], baseHeight, z / (float)last), n);
                case LandSide.South: // shares the neighbour's bottom row as its top row
                    return HeightField.FromFunction((x, z) => Lerp(neighbour[x, 0], baseHeight, (last - z) / (float)last), n);
                case LandSide.East: // shares the neighbour's right column as its left column
                    return HeightField.FromFunction((x, z) => Lerp(neighbour[last, z], baseHeight, x / (float)last), n);
                case LandSide.West: // shares the neighbour's left column as its right column
                    return HeightField.FromFunction((x, z) => Lerp(neighbour[0, z], baseHeight, (last - x) / (float)last), n);
                default: throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        static float Lerp(float from, float to, float t) => from + (to - from) * t;

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
