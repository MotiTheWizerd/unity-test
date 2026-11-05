using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Data.Terrain;

namespace Forge.Tests.Data
{
    /// <summary>A terrain file kept in memory, for serializer tests: each distinct heights or paint instance is a block.</summary>
    sealed class MemoryTerrain : ITerrainBlockWriter, ITerrainBlockReader
    {
        public const string FileName = "test.0000000000000000.terrain";

        readonly List<object> _blocks = new List<object>();

        /// <summary>A terrain file holding <paramref name="plugin"/>'s chunk heights and paint, or null when it has none.</summary>
        public static TerrainLink For(Plugin plugin, MemoryTerrain terrain)
        {
            var chunks = plugin.Records.OfType<TerrainChunkRecord>().ToList();
            if (chunks.Count == 0)
                return null;

            foreach (var block in chunks.Select(c => (object)c.Heights).Concat(chunks.Where(c => c.Paint != null).Select(c => c.Paint)))
            {
                if (!terrain._blocks.Contains(block))
                    terrain._blocks.Add(block);
            }

            return new TerrainLink(FileName, terrain);
        }

        public int BlockCount => _blocks.Count;

        public int BlockOf(ChunkHeights heights) => _blocks.IndexOf(heights);

        public int BlockOf(ChunkPaint paint) => _blocks.IndexOf(paint);

        public ChunkHeights Block(int index, int resolution)
        {
            if (index < 0 || index >= _blocks.Count || !(_blocks[index] is ChunkHeights heights) || heights.Resolution != resolution)
                throw new DataValidationException(new[] { $"No heights block {index} at resolution {resolution}." });
            return heights;
        }

        public ChunkPaint Paint(int index, IReadOnlyList<RecordId> layers)
        {
            if (index < 0 || index >= _blocks.Count || !(_blocks[index] is ChunkPaint paint) || !paint.Layers.SequenceEqual(layers))
                throw new DataValidationException(new[] { $"No paint block {index} with those layers." });
            return paint;
        }
    }
}
