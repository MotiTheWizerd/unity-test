using System;

namespace Forge.Data.Terrain
{
    /// <summary>
    /// Immutable square grid of terrain heights in meters, row-major by Z then X.
    /// </summary>
    public sealed class HeightField
    {
        readonly float[] _heights;

        public HeightField(int resolution, float[] heights)
        {
            if (!WorldGrid.IsValidHeightmapResolution(resolution))
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Resolution must be 2^n + 1 within the supported range.");
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

            Resolution = resolution;
            _heights = (float[])heights.Clone();
        }

        public int Resolution { get; }

        public float this[int x, int z] => _heights[z * Resolution + x];

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
    }
}
