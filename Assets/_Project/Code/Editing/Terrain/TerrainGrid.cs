using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Geometry;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// A worldspace's terrain as one grid of height samples across all its chunks, read from the terrain records.
    /// Neighbouring chunks share their edge samples, so each global sample (gx, gz) has one height. Engine-free:
    /// it answers for any position, whether or not the scene has that chunk loaded. A chunk's heights are read the
    /// first time a sample of it is asked for, then held for the life of the grid (which lasts one world version).
    /// </summary>
    public sealed class TerrainGrid
    {
        readonly Dictionary<ChunkCoord, TerrainChunkRecord> _chunks = new Dictionary<ChunkCoord, TerrainChunkRecord>();
        readonly Dictionary<ChunkCoord, HeightField> _loaded = new Dictionary<ChunkCoord, HeightField>();

        /// <param name="space">The worldspace whose land this is; other spaces' chunks are left out.</param>
        public TerrainGrid(IRecordStore store, RecordId space)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            Space = space;
            // The lowest id wins if two plugins define the same coordinate, as in the world view.
            foreach (var chunk in store.OfType<TerrainChunkRecord>().Where(c => c.Space == space).OrderBy(c => c.Id))
            {
                if (!_chunks.ContainsKey(chunk.Coord))
                    _chunks.Add(chunk.Coord, chunk);
            }

            var resolutions = _chunks.Values.Select(c => c.Heights.Resolution).Distinct().ToList();
            if (resolutions.Count > 1)
                throw new InvalidOperationException("Every terrain chunk must share one heightmap resolution.");

            Resolution = resolutions.Count == 1 ? resolutions[0] : WorldGrid.DefaultHeightmapResolution;
            Spacing = WorldGrid.ChunkSize / (Resolution - 1);
        }

        public RecordId Space { get; }

        /// <summary>Samples along each chunk edge.</summary>
        public int Resolution { get; }

        /// <summary>Meters between neighbouring samples.</summary>
        public float Spacing { get; }

        public int CellsPerChunk => Resolution - 1;

        public bool Contains(ChunkCoord coord) => _chunks.ContainsKey(coord);

        public bool TryGetChunk(ChunkCoord coord, out TerrainChunkRecord chunk) => _chunks.TryGetValue(coord, out chunk);

        /// <summary>The heights of the chunk at <paramref name="coord"/>, read once per grid; null where there is none.</summary>
        public HeightField HeightsOf(ChunkCoord coord)
        {
            if (_loaded.TryGetValue(coord, out var heights))
                return heights;
            if (!_chunks.TryGetValue(coord, out var chunk))
                return null;

            heights = chunk.Heights.Load();
            _loaded.Add(coord, heights);
            return heights;
        }

        /// <summary>The chunk that holds global sample (gx, gz), and the sample's index inside it.</summary>
        public (ChunkCoord Coord, int X, int Z) Locate(int gx, int gz)
        {
            var coord = new ChunkCoord(FloorDiv(gx, CellsPerChunk), FloorDiv(gz, CellsPerChunk));
            return (coord, gx - coord.X * CellsPerChunk, gz - coord.Z * CellsPerChunk);
        }

        /// <summary>Height of global sample (gx, gz), or null where no chunk holds it.</summary>
        public float? SampleHeight(int gx, int gz)
        {
            var (coord, x, z) = Locate(gx, gz);
            return HeightsOf(coord) is HeightField heights ? heights[x, z] : (float?)null;
        }

        /// <summary>Height at a ground position (x, z), bilinear between samples; null outside the terrain.</summary>
        public float? HeightAt(Vector2 ground) => Bilinear(ground, Spacing, SampleHeight);

        /// <summary>
        /// How steep the ground is at <paramref name="ground"/>, in degrees, from the heights a sample's width to each
        /// side; null outside the terrain.
        /// </summary>
        public float? SlopeAt(Vector2 ground)
        {
            var half = Spacing / 2f;
            var east = HeightAt(ground + new Vector2(half, 0f));
            var west = HeightAt(ground - new Vector2(half, 0f));
            var north = HeightAt(ground + new Vector2(0f, half));
            var south = HeightAt(ground - new Vector2(0f, half));
            if (east == null || west == null || north == null || south == null)
                return null;

            var dx = (east.Value - west.Value) / Spacing;
            var dz = (north.Value - south.Value) / Spacing;
            return (float)(Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);
        }

        /// <summary>Every terrain sample in the square of half-edge <paramref name="reach"/> around <paramref name="center"/>.</summary>
        public IEnumerable<(int Gx, int Gz, Vector2 Ground, float Height)> SamplesAround(Vector2 center, float reach) =>
            SamplesIn(center - new Vector2(reach), center + new Vector2(reach));

        /// <summary>Every terrain sample in the box from <paramref name="min"/> (south-west) to <paramref name="max"/> (north-east).</summary>
        public IEnumerable<(int Gx, int Gz, Vector2 Ground, float Height)> SamplesIn(Vector2 min, Vector2 max)
        {
            var firstX = (int)Math.Ceiling(min.X / Spacing);
            var lastX = (int)Math.Floor(max.X / Spacing);
            var firstZ = (int)Math.Ceiling(min.Y / Spacing);
            var lastZ = (int)Math.Floor(max.Y / Spacing);
            for (var gz = firstZ; gz <= lastZ; gz++)
            for (var gx = firstX; gx <= lastX; gx++)
            {
                if (SampleHeight(gx, gz) is float height)
                    yield return (gx, gz, new Vector2(gx * Spacing, gz * Spacing), height);
            }
        }

        /// <summary>
        /// The mean height of the terrain samples inside <paramref name="area"/>, or the height at its center when it
        /// is too small to hold a sample; null where there is no terrain.
        /// </summary>
        public float? AverageHeight(GroundRect area)
        {
            var sum = 0.0;
            var count = 0;
            foreach (var sample in SamplesAround(area.Center, area.Reach))
            {
                if (area.DistanceOutside(sample.Ground) > 0f)
                    continue;
                sum += sample.Height;
                count++;
            }

            return count > 0 ? (float)(sum / count) : HeightAt(area.Center);
        }

        /// <summary>
        /// The height at <paramref name="ground"/>, bilinear between the four global samples around it as
        /// <paramref name="sample"/> reads them; null if any of them is missing.
        /// </summary>
        public static float? Bilinear(Vector2 ground, float spacing, Func<int, int, float?> sample)
        {
            if (sample == null)
                throw new ArgumentNullException(nameof(sample));

            var gx = ground.X / spacing;
            var gz = ground.Y / spacing;
            var x0 = (int)Math.Floor(gx);
            var z0 = (int)Math.Floor(gz);
            var fx = gx - x0;
            var fz = gz - z0;

            var h00 = sample(x0, z0);
            var h10 = sample(x0 + 1, z0);
            var h01 = sample(x0, z0 + 1);
            var h11 = sample(x0 + 1, z0 + 1);
            if (h00 == null || h10 == null || h01 == null || h11 == null)
                return null;

            var top = h00.Value + (h10.Value - h00.Value) * fx;
            var bottom = h01.Value + (h11.Value - h01.Value) * fx;
            return top + (bottom - top) * fz;
        }

        static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
    }
}
