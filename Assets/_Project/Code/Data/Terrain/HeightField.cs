using System;

namespace Forge.Data.Terrain
{
    /// <summary>
    /// Immutable square grid of terrain heights in meters, row-major by Z then X: a chunk's heights held in memory.
    /// </summary>
    public sealed class HeightField : ChunkHeights
    {
        readonly float[] _heights;

        public HeightField(int resolution, float[] heights)
            : base(resolution)
        {
            if (heights == null)
                throw new ArgumentNullException(nameof(heights));
            if (heights.Length != resolution * resolution)
                throw new ArgumentException($"Expected {resolution * resolution} heights but got {heights.Length}.", nameof(heights));

            for (var i = 0; i < heights.Length; i++)
            {
                var h = heights[i];
                if (float.IsNaN(h) || h < 0f || h > WorldGrid.MaxTerrainHeight)
                    throw new ArgumentOutOfRangeException(nameof(heights), h, $"Height at index {i} must be within [0, {WorldGrid.MaxTerrainHeight}].");
            }

            _heights = (float[])heights.Clone();
        }

        // Takes ownership of samples already known to be valid (decoded from a terrain file), without a copy.
        HeightField(float[] heights, int resolution)
            : base(resolution)
        {
            _heights = heights;
        }

        public float this[int x, int z] => _heights[z * Resolution + x];

        public override HeightField Load() => this;

        public static HeightField Flat(float height, int resolution = WorldGrid.DefaultHeightmapResolution)
        {
            var heights = new float[resolution * resolution];
            for (var i = 0; i < heights.Length; i++)
                heights[i] = height;
            return new HeightField(resolution, heights);
        }

        /// <summary>Builds a field by sampling <paramref name="heightAt"/> at each (x, z) sample index.</summary>
        public static HeightField FromFunction(Func<int, int, float> heightAt, int resolution = WorldGrid.DefaultHeightmapResolution)
        {
            if (heightAt == null)
                throw new ArgumentNullException(nameof(heightAt));

            var heights = new float[resolution * resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                heights[z * resolution + x] = heightAt(x, z);
            return new HeightField(resolution, heights);
        }

        /// <summary>Copy of the samples, row-major by Z then X.</summary>
        public float[] ToArray() => (float[])_heights.Clone();

        /// <summary>Wraps decoded samples, which the caller guarantees are in range, without copying or checking them.</summary>
        internal static HeightField Trusted(int resolution, float[] heights) => new HeightField(heights, resolution);
    }
}
