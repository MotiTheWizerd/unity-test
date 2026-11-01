namespace Forge.Data.Terrain
{
    /// <summary>World-wide terrain constants shared by data, simulation and presentation.</summary>
    public static class WorldGrid
    {
        /// <summary>Edge length of a terrain chunk in meters.</summary>
        public const float ChunkSize = 64f;

        /// <summary>Highest terrain height in meters; heights are stored in [0, MaxTerrainHeight].</summary>
        public const float MaxTerrainHeight = 200f;

        /// <summary>Default samples per chunk edge (2^n + 1, so neighbours share edge samples).</summary>
        public const int DefaultHeightmapResolution = 65;

        public const int MinHeightmapResolution = 17;

        public const int MaxHeightmapResolution = 1025;

        public static bool IsValidHeightmapResolution(int resolution)
        {
            if (resolution < MinHeightmapResolution || resolution > MaxHeightmapResolution)
                return false;
            var cells = resolution - 1;
            return (cells & (cells - 1)) == 0;
        }
    }
}
