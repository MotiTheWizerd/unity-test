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
        readonly Dictionary<ChunkCoord, TerrainChunkRecord> _originals;
        readonly Dictionary<ChunkCoord, float[]> _working = new Dictionary<ChunkCoord, float[]>();
        readonly HashSet<ChunkCoord> _touched = new HashSet<ChunkCoord>();
        readonly int _resolution;
        readonly float _spacing;

        /// <param name="store">World to sculpt; its terrain is snapshotted now.</param>
        /// <param name="brush">Brush used for every dab of this stroke.</param>
        /// <param name="start">World XZ where the stroke began (sets the Flatten target).</param>
        public SculptStroke(IRecordStore store, BrushSettings brush, Vector2 start)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            Brush = brush ?? throw new ArgumentNullException(nameof(brush));

            // Last writer wins if two plugins define the same coordinate, matching the world view.
            _originals = new Dictionary<ChunkCoord, TerrainChunkRecord>();
            foreach (var chunk in store.OfType<TerrainChunkRecord>().OrderBy(c => c.Id))
            {
                if (!_originals.ContainsKey(chunk.Coord))
                    _originals.Add(chunk.Coord, chunk);
            }

            var resolutions = _originals.Values.Select(c => c.Heights.Resolution).Distinct().ToList();
            if (resolutions.Count > 1)
                throw new InvalidOperationException("Sculpting requires every terrain chunk to share one heightmap resolution.");

            _resolution = resolutions.Count == 1 ? resolutions[0] : WorldGrid.DefaultHeightmapResolution;
            _spacing = WorldGrid.ChunkSize / (_resolution - 1);
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

        float? Sample(Vector2 world, bool original)
        {
            var gx = world.X / _spacing;
            var gz = world.Y / _spacing;
            var x0 = (int)Math.Floor(gx);
            var z0 = (int)Math.Floor(gz);
            var fx = gx - x0;
            var fz = gz - z0;

            var h00 = GlobalHeight(x0, z0, original);
            var h10 = GlobalHeight(x0 + 1, z0, original);
            var h01 = GlobalHeight(x0, z0 + 1, original);
            var h11 = GlobalHeight(x0 + 1, z0 + 1, original);
            if (h00 == null || h10 == null || h01 == null || h11 == null)
                return null;

            var top = h00.Value + (h10.Value - h00.Value) * fx;
            var bottom = h01.Value + (h11.Value - h01.Value) * fx;
            return top + (bottom - top) * fz;
        }

        /// <summary>Applies one dab centered at world XZ. Returns the chunks it changed.</summary>
        public IReadOnlyCollection<ChunkCoord> ApplyDab(Vector2 center)
        {
            var radius = Brush.Radius;
            var minX = (int)Math.Floor((center.X - radius) / _spacing);
            var maxX = (int)Math.Ceiling((center.X + radius) / _spacing);
            var minZ = (int)Math.Floor((center.Y - radius) / _spacing);
            var maxZ = (int)Math.Ceiling((center.Y + radius) / _spacing);

            // Pass 1: compute every new value from the pre-dab state.
            var updates = new List<(int gx, int gz, float height)>();
            for (var gz = minZ; gz <= maxZ; gz++)
            for (var gx = minX; gx <= maxX; gx++)
            {
                var current = GlobalHeight(gx, gz);
                if (current == null)
                    continue;

                var distance = Vector2.Distance(center, new Vector2(gx * _spacing, gz * _spacing));
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
        public HeightField HeightsOf(ChunkCoord coord)
        {
            if (!_originals.TryGetValue(coord, out var original))
                throw new KeyNotFoundException($"No terrain chunk at {coord}.");
            return _working.TryGetValue(coord, out var heights) ? new HeightField(_resolution, heights) : original.Heights;
        }

        /// <summary>Records for every chunk this stroke changed, ready to commit.</summary>
        public IReadOnlyList<TerrainChunkRecord> ChangedRecords() =>
            _touched.Select(c => _originals[c].WithHeights(HeightsOf(c))).OrderBy(r => r.Id).ToList();

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
            var cells = _resolution - 1;
            var coord = new ChunkCoord(FloorDiv(gx, cells), FloorDiv(gz, cells));
            if (!_originals.TryGetValue(coord, out var chunk))
                return null;

            var x = gx - coord.X * cells;
            var z = gz - coord.Z * cells;
            return !original && _working.TryGetValue(coord, out var heights) ? heights[z * _resolution + x] : chunk.Heights[x, z];
        }

        // A global sample on a chunk edge or corner belongs to up to four chunks.
        IEnumerable<(ChunkCoord coord, int index)> Owners(int gx, int gz)
        {
            var cells = _resolution - 1;
            var cx = FloorDiv(gx, cells);
            var cz = FloorDiv(gz, cells);
            var onXEdge = gx - cx * cells == 0;
            var onZEdge = gz - cz * cells == 0;

            for (var ox = onXEdge ? -1 : 0; ox <= 0; ox++)
            for (var oz = onZEdge ? -1 : 0; oz <= 0; oz++)
            {
                var coord = new ChunkCoord(cx + ox, cz + oz);
                if (!_originals.ContainsKey(coord))
                    continue;
                var x = gx - coord.X * cells;
                var z = gz - coord.Z * cells;
                yield return (coord, z * _resolution + x);
            }
        }

        float[] WorkingFor(ChunkCoord coord)
        {
            if (!_working.TryGetValue(coord, out var heights))
                _working[coord] = heights = _originals[coord].Heights.ToArray();
            return heights;
        }

        static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
    }
}
