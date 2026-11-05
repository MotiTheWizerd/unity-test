using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>A terrain file as a plugin serializer reads it: the chunk heights stored in each numbered block.</summary>
    public interface ITerrainBlockReader
    {
        /// <summary>The heights stored in block <paramref name="index"/>, read when first needed.</summary>
        /// <exception cref="DataValidationException">There is no such block, or it holds another resolution.</exception>
        ChunkHeights Block(int index, int resolution);
    }

    /// <summary>A terrain file as a plugin serializer writes it: the block each chunk's heights went into.</summary>
    public interface ITerrainBlockWriter
    {
        /// <summary>The block <paramref name="heights"/> were written to.</summary>
        int BlockOf(ChunkHeights heights);
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
