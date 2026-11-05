using System;

namespace Forge.Data.Terrain
{
    /// <summary>World-wide terrain constants shared by data, simulation and presentation.</summary>
    public static class WorldGrid
    {
        /// <summary>Edge length of a terrain chunk in meters.</summary>
        public const float ChunkSize = 64f;

        /// <summary>Highest terrain height in meters; heights are stored in [0, MaxTerrainHeight].</summary>
        public const float MaxTerrainHeight = 200f;

        /// <summary>
        /// Height new land starts at, in meters. Heights can't go below 0, so land starts well above it: room to dig
        /// lakes and valleys down, and the rest of <see cref="MaxTerrainHeight"/> to raise hills.
        /// </summary>
        public const float DefaultLandHeight = 50f;

        /// <summary>Default samples per chunk edge (2^n + 1, so neighbours share edge samples).</summary>
        public const int DefaultHeightmapResolution = 65;

        public const int MinHeightmapResolution = 17;

        public const int MaxHeightmapResolution = 1025;

        /// <summary>The chunk containing world position (x, z). Chunk (0, 0) spans [0, ChunkSize) on both axes.</summary>
        public static ChunkCoord ChunkOf(float x, float z) =>
            new ChunkCoord((int)Math.Floor(x / ChunkSize), (int)Math.Floor(z / ChunkSize));

        public static bool IsValidHeightmapResolution(int resolution)
        {
            if (resolution < MinHeightmapResolution || resolution > MaxHeightmapResolution)
                return false;
            var cells = resolution - 1;
            return (cells & (cells - 1)) == 0;
        }

        /// <summary>Meters between neighbouring samples of a chunk with <paramref name="resolution"/> samples per edge.</summary>
        public static float SpacingOf(int resolution)
        {
            if (!IsValidHeightmapResolution(resolution))
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Resolution must be 2^n + 1 within the supported range.");
            return ChunkSize / (resolution - 1);
        }

        /// <summary>Samples per chunk edge for <paramref name="spacing"/> meters between samples (1 m → 65, 2 m → 33).</summary>
        public static int ResolutionFor(float spacing)
        {
            var cells = ChunkSize / spacing;
            var resolution = (int)Math.Round(cells) + 1;
            if (!(spacing > 0f) || Math.Abs(cells - Math.Round(cells)) > 1e-4 || !IsValidHeightmapResolution(resolution))
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing, $"Spacing must divide the chunk ({ChunkSize} m) into a power of two of cells, e.g. 1, 2 or 4 m.");
            return resolution;
        }
    }
}
