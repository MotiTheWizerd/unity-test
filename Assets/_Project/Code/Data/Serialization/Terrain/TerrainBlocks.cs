using System.Collections.Generic;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>A terrain file as a plugin serializer reads it: the chunk heights and paint stored in each numbered block.</summary>
    public interface ITerrainBlockReader
    {
        /// <summary>The heights stored in block <paramref name="index"/>, read when first needed.</summary>
        /// <exception cref="DataValidationException">There is no such block, it holds paint, or another resolution.</exception>
        ChunkHeights Block(int index, int resolution);

        /// <summary>The paint stored in block <paramref name="index"/> with <paramref name="layers"/>, read when first needed.</summary>
        /// <exception cref="DataValidationException">There is no such block, or it holds heights.</exception>
        ChunkPaint Paint(int index, IReadOnlyList<RecordId> layers);
    }

    /// <summary>A terrain file as a plugin serializer writes it: the block each chunk's heights and paint went into.</summary>
    public interface ITerrainBlockWriter
    {
        /// <summary>The block <paramref name="heights"/> were written to.</summary>
        int BlockOf(ChunkHeights heights);

        /// <summary>The block <paramref name="paint"/> was written to.</summary>
        int BlockOf(ChunkPaint paint);
    }

    /// <summary>The terrain file a plugin's JSON names (a file name, no folder) and the blocks its chunks went into.</summary>
    public sealed class TerrainLink
    {
        public TerrainLink(string fileName, ITerrainBlockWriter blocks)
        {
            FileName = fileName;
            Blocks = blocks;
        }

        public string FileName { get; }

        public ITerrainBlockWriter Blocks { get; }
    }
}
