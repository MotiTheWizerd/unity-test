using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// How high a space's land reaches, so a snow line can be set against it. Each chunk's highest point is read once
    /// per height data: repainting a chunk keeps its heights, so only sculpted chunks are read again.
    /// </summary>
    public static class LandHeights
    {
        static readonly ConditionalWeakTable<ChunkHeights, object> Highest = new ConditionalWeakTable<ChunkHeights, object>();

        /// <summary>The highest ground of <paramref name="space"/>, in meters; null when it has no land.</summary>
        public static float? HighestIn(IRecordStore store, RecordId space)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var grid = new TerrainGrid(store, space);
            float? highest = null;
            foreach (var chunk in store.OfType<TerrainChunkRecord>()
                         .Where(chunk => chunk.Space == space && grid.TryGetChunk(chunk.Coord, out var winner) && winner.Id == chunk.Id))
            {
                var top = (float)Highest.GetValue(chunk.Heights, heights => HighestOf(heights.Load()));
                if (!highest.HasValue || top > highest.Value)
                    highest = top;
            }

            return highest;
        }

        static object HighestOf(HeightField heights)
        {
            var top = 0f;
            for (var z = 0; z < heights.Resolution; z++)
            for (var x = 0; x < heights.Resolution; x++)
                top = Math.Max(top, heights[x, z]);
            return top;
        }
    }
}
