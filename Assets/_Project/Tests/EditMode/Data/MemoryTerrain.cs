using System.Collections.Generic;
using System.Linq;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Data.Terrain;

namespace Forge.Tests.Data
{
    /// <summary>A terrain file kept in memory, for serializer tests: each distinct heights instance is a block.</summary>
    sealed class MemoryTerrain : ITerrainBlockWriter, ITerrainBlockReader
    {
        public const string FileName = "test.0000000000000000.terrain";

        readonly List<ChunkHeights> _blocks = new List<ChunkHeights>();

        /// <summary>A terrain file holding <paramref name="plugin"/>'s chunk heights, or null when it has none.</summary>
        public static TerrainLink For(Plugin plugin, MemoryTerrain terrain)
        {
            var chunks = plugin.Records.OfType<TerrainChunkRecord>().ToList();
            if (chunks.Count == 0)
                return null;

            foreach (var chunk in chunks.Where(c => !terrain._blocks.Contains(c.Heights)))
                terrain._blocks.Add(chunk.Heights);
            return new TerrainLink(FileName, terrain);
        }

        public int BlockCount => _blocks.Count;

        public int BlockOf(ChunkHeights heights) => _blocks.IndexOf(heights);

        public ChunkHeights Block(int index, int resolution)
        {
            if (index < 0 || index >= _blocks.Count || _blocks[index].Resolution != resolution)
                throw new Forge.Data.DataValidationException(new[] { $"No block {index} at resolution {resolution}." });
            return _blocks[index];
        }
    }
}
