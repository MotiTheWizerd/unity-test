using System;

namespace Forge.Data.Terrain
{
    /// <summary>
    /// A terrain chunk's heights, wherever they are: in memory (a <see cref="HeightField"/>: new or edited land) or
    /// stored in a plugin's terrain file and read from disk the first time they are needed, so a world opens without
    /// reading its land and keeps only the land in use in memory. <see cref="Resolution"/> is known without loading.
    /// </summary>
    public abstract class ChunkHeights
    {
        protected ChunkHeights(int resolution)
        {
            if (!WorldGrid.IsValidHeightmapResolution(resolution))
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Resolution must be 2^n + 1 within the supported range.");
            Resolution = resolution;
        }

        /// <summary>Samples along each chunk edge.</summary>
        public int Resolution { get; }

        /// <summary>
        /// The heights. Stored heights are read and decoded on first use and kept in a shared cache, so this may touch
        /// the disk: load once per use (a brush stroke, a chunk shown), not once per sample.
        /// </summary>
        /// <exception cref="DataValidationException">The terrain file is missing or damaged.</exception>
        public abstract HeightField Load();
    }
}
