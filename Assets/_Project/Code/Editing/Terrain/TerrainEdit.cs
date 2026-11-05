using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// A working copy of the terrain that an edit (a brush stroke, a levelled area) changes until it is
    /// committed (as one undo step, see <see cref="TerrainEdits"/>) or discarded. Works on the global sample
    /// grid, so samples shared by neighbouring chunks always receive identical values and seams never open.
    /// </summary>
    public abstract class TerrainEdit
    {
        readonly Dictionary<ChunkCoord, float[]> _working = new Dictionary<ChunkCoord, float[]>();
        readonly HashSet<ChunkCoord> _touched = new HashSet<ChunkCoord>();

        /// <param name="store">World to edit; the terrain of <paramref name="space"/> is snapshotted now.</param>
        /// <param name="space">The worldspace whose land is edited.</param>
        protected TerrainEdit(IRecordStore store, RecordId space)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            Terrain = new TerrainGrid(store, space);
        }

        /// <summary>Names the edit in the undo history, e.g. "Sculpt (Raise)".</summary>
        public abstract string Description { get; }

        /// <summary>Chunks changed so far.</summary>
        public IReadOnlyCollection<ChunkCoord> TouchedChunks => _touched;

        /// <summary>The terrain as it was before the edit.</summary>
        protected TerrainGrid Terrain { get; }

        /// <summary>Current (working) height at a world XZ position, bilinearly interpolated; null outside the terrain.</summary>
        public float? SampleHeight(Vector2 world) => Sample(world, original: false);

        /// <summary>Height at a world XZ position as it was before the edit; null outside the terrain.</summary>
        public float? OriginalHeight(Vector2 world) => Sample(world, original: true);

        /// <summary>
        /// How far the edit has raised (positive) or lowered (negative) the ground at a
        /// world XZ position so far; null outside the terrain.
        /// </summary>
        public float? HeightChangeAt(Vector2 world) => Sample(world, original: false) - Sample(world, original: true);

        float? Sample(Vector2 world, bool original) =>
            TerrainGrid.Bilinear(world, Terrain.Spacing, (gx, gz) => GlobalHeight(gx, gz, original));

        /// <summary>Current working heights of a chunk (original heights if untouched).</summary>
        public HeightField HeightsOf(ChunkCoord coord) =>
            _working.TryGetValue(coord, out var heights) ? new HeightField(Terrain.Resolution, heights) : OriginalHeights(coord);

        /// <summary>Records for every chunk this edit changed, ready to commit.</summary>
        public IReadOnlyList<TerrainChunkRecord> ChangedRecords() =>
            _touched.Select(c => Original(c).WithHeights(HeightsOf(c))).OrderBy(r => r.Id).ToList();

        /// <summary>Working height of global sample (gx, gz), or its height before the edit; null where no chunk holds it.</summary>
        protected float? GlobalHeight(int gx, int gz, bool original = false)
        {
            var (coord, x, z) = Terrain.Locate(gx, gz);
            if (!original && _working.TryGetValue(coord, out var heights))
                return heights[z * Terrain.Resolution + x];
            return Terrain.HeightsOf(coord) is HeightField stored ? stored[x, z] : (float?)null;
        }

        /// <summary>
        /// Writes new heights (clamped to the terrain's range) to global samples, each into every chunk that
        /// holds it. Every value is computed beforehand, from the state before the write. Returns the chunks it changed.
        /// </summary>
        protected IReadOnlyCollection<ChunkCoord> Write(IReadOnlyList<(int gx, int gz, float height)> samples)
        {
            var changed = new HashSet<ChunkCoord>();
            foreach (var (gx, gz, height) in samples)
            {
                var clamped = Clamp(height);
                if (GlobalHeight(gx, gz) is float current && current == clamped)
                    continue;

                foreach (var (coord, index) in Owners(gx, gz))
                {
                    WorkingFor(coord)[index] = clamped;
                    changed.Add(coord);
                }
            }

            _touched.UnionWith(changed);
            return changed;
        }

        static float Clamp(float height) => Math.Max(0f, Math.Min(WorldGrid.MaxTerrainHeight, height));

        TerrainChunkRecord Original(ChunkCoord coord) =>
            Terrain.TryGetChunk(coord, out var chunk) ? chunk : throw new KeyNotFoundException($"No terrain chunk at {coord}.");

        HeightField OriginalHeights(ChunkCoord coord) =>
            Terrain.HeightsOf(coord) ?? throw new KeyNotFoundException($"No terrain chunk at {coord}.");

        // A global sample on a chunk edge or corner belongs to up to four chunks.
        IEnumerable<(ChunkCoord coord, int index)> Owners(int gx, int gz)
        {
            var cells = Terrain.CellsPerChunk;
            var (home, homeX, homeZ) = Terrain.Locate(gx, gz);
            var onXEdge = homeX == 0;
            var onZEdge = homeZ == 0;

            for (var ox = onXEdge ? -1 : 0; ox <= 0; ox++)
            for (var oz = onZEdge ? -1 : 0; oz <= 0; oz++)
            {
                var coord = new ChunkCoord(home.X + ox, home.Z + oz);
                if (!Terrain.Contains(coord))
                    continue;
                var x = gx - coord.X * cells;
                var z = gz - coord.Z * cells;
                yield return (coord, z * Terrain.Resolution + x);
            }
        }

        float[] WorkingFor(ChunkCoord coord)
        {
            if (!_working.TryGetValue(coord, out var heights))
                _working[coord] = heights = OriginalHeights(coord).ToArray();
            return heights;
        }
    }
}
