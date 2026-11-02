using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// One brush stroke: a working copy of the terrain that dabs modify until the
    /// stroke is committed (as one undo step, see <see cref="TerrainEdits"/>) or
    /// discarded. Works on the global sample grid, so samples shared by
    /// neighbouring chunks always receive identical values and seams never open.
    /// </summary>
    public sealed class SculptStroke
    {
        readonly TerrainGrid _terrain;
        readonly Dictionary<ChunkCoord, float[]> _working = new Dictionary<ChunkCoord, float[]>();
        readonly HashSet<ChunkCoord> _touched = new HashSet<ChunkCoord>();

        /// <param name="store">World to sculpt; its terrain is snapshotted now.</param>
        /// <param name="brush">Brush used for every dab of this stroke.</param>
        /// <param name="start">World XZ where the stroke began (sets the Flatten target).</param>
        public SculptStroke(IRecordStore store, BrushSettings brush, Vector2 start)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            Brush = brush ?? throw new ArgumentNullException(nameof(brush));

            _terrain = new TerrainGrid(store);
            FlattenHeight = SampleHeight(start) ?? 0f;
        }

        public BrushSettings Brush { get; }

        public float FlattenHeight { get; }

        /// <summary>Chunks changed by at least one dab so far.</summary>
        public IReadOnlyCollection<ChunkCoord> TouchedChunks => _touched;

        /// <summary>Current (working) height at a world XZ position, bilinearly interpolated; null outside the terrain.</summary>
        public float? SampleHeight(Vector2 world) => Sample(world, original: false);

        /// <summary>Height at a world XZ position as it was before the stroke; null outside the terrain.</summary>
        public float? OriginalHeight(Vector2 world) => Sample(world, original: true);

        /// <summary>
        /// How far the stroke has raised (positive) or lowered (negative) the ground at a
        /// world XZ position so far; null outside the terrain.
        /// </summary>
        public float? HeightChangeAt(Vector2 world) => Sample(world, original: false) - Sample(world, original: true);

        float? Sample(Vector2 world, bool original) =>
            TerrainGrid.Bilinear(world, _terrain.Spacing, (gx, gz) => GlobalHeight(gx, gz, original));

        /// <summary>Applies one dab centered at world XZ. Returns the chunks it changed.</summary>
        public IReadOnlyCollection<ChunkCoord> ApplyDab(Vector2 center)
        {
            var radius = Brush.Radius;
            var spacing = _terrain.Spacing;
            var minX = (int)Math.Floor((center.X - radius) / spacing);
            var maxX = (int)Math.Ceiling((center.X + radius) / spacing);
            var minZ = (int)Math.Floor((center.Y - radius) / spacing);
            var maxZ = (int)Math.Ceiling((center.Y + radius) / spacing);

            // Pass 1: compute every new value from the pre-dab state.
            var updates = new List<(int gx, int gz, float height)>();
            for (var gz = minZ; gz <= maxZ; gz++)
            for (var gx = minX; gx <= maxX; gx++)
            {
                var current = GlobalHeight(gx, gz);
                if (current == null)
                    continue;

                var distance = Vector2.Distance(center, new Vector2(gx * spacing, gz * spacing));
                var weight = Brush.Weight(distance) * Brush.Strength;
                if (weight <= 0f)
                    continue;

                var next = Clamp(Apply(current.Value, weight, gx, gz));
                if (next != current.Value)
                    updates.Add((gx, gz, next));
            }

            // Pass 2: write each global sample into every chunk that contains it.
            var changed = new HashSet<ChunkCoord>();
            foreach (var (gx, gz, height) in updates)
            {
                foreach (var (coord, index) in Owners(gx, gz))
                {
                    WorkingFor(coord)[index] = height;
                    changed.Add(coord);
                }
            }

            _touched.UnionWith(changed);
            return changed;
        }

        /// <summary>Current working heights of a chunk (original heights if untouched).</summary>
        public HeightField HeightsOf(ChunkCoord coord) =>
            _working.TryGetValue(coord, out var heights) ? new HeightField(_terrain.Resolution, heights) : Original(coord).Heights;

        /// <summary>Records for every chunk this stroke changed, ready to commit.</summary>
        public IReadOnlyList<TerrainChunkRecord> ChangedRecords() =>
            _touched.Select(c => Original(c).WithHeights(HeightsOf(c))).OrderBy(r => r.Id).ToList();

        TerrainChunkRecord Original(ChunkCoord coord) =>
            _terrain.TryGetChunk(coord, out var chunk) ? chunk : throw new KeyNotFoundException($"No terrain chunk at {coord}.");

        float Apply(float current, float weight, int gx, int gz)
        {
            switch (Brush.Mode)
            {
                case BrushMode.Raise:
                    return current + BrushSettings.MaxDabHeight * weight;
                case BrushMode.Lower:
                    return current - BrushSettings.MaxDabHeight * weight;
                case BrushMode.Flatten:
                    return current + (FlattenHeight - current) * weight;
                case BrushMode.Smooth:
                    var average = NeighbourAverage(gx, gz) ?? current;
                    return current + (average - current) * weight;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Brush.Mode), Brush.Mode, null);
            }
        }

        float? NeighbourAverage(int gx, int gz)
        {
            var sum = 0f;
            var count = 0;
            for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var h = GlobalHeight(gx + dx, gz + dz);
                if (h == null)
                    continue;
                sum += h.Value;
                count++;
            }

            return count > 0 ? sum / count : (float?)null;
        }

        static float Clamp(float height) => Math.Max(0f, Math.Min(WorldGrid.MaxTerrainHeight, height));

        float? GlobalHeight(int gx, int gz, bool original = false)
        {
            var (coord, x, z) = _terrain.Locate(gx, gz);
            if (!_terrain.TryGetChunk(coord, out var chunk))
                return null;
            return !original && _working.TryGetValue(coord, out var heights) ? heights[z * _terrain.Resolution + x] : chunk.Heights[x, z];
        }

        // A global sample on a chunk edge or corner belongs to up to four chunks.
        IEnumerable<(ChunkCoord coord, int index)> Owners(int gx, int gz)
        {
            var cells = _terrain.CellsPerChunk;
            var (home, homeX, homeZ) = _terrain.Locate(gx, gz);
            var onXEdge = homeX == 0;
            var onZEdge = homeZ == 0;

            for (var ox = onXEdge ? -1 : 0; ox <= 0; ox++)
            for (var oz = onZEdge ? -1 : 0; oz <= 0; oz++)
            {
                var coord = new ChunkCoord(home.X + ox, home.Z + oz);
                if (!_terrain.Contains(coord))
                    continue;
                var x = gx - coord.X * cells;
                var z = gz - coord.Z * cells;
                yield return (coord, z * _terrain.Resolution + x);
            }
        }

        float[] WorkingFor(ChunkCoord coord)
        {
            if (!_working.TryGetValue(coord, out var heights))
                _working[coord] = heights = Original(coord).Heights.ToArray();
            return heights;
        }
    }
}
