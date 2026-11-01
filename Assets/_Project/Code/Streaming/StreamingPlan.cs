using System.Collections.Generic;
using Forge.Data.Terrain;

namespace Forge.Streaming
{
    /// <summary>Result of <see cref="ChunkWindow.Plan"/>: which chunks to load (nearest first) and unload.</summary>
    public sealed class StreamingPlan
    {
        public StreamingPlan(IReadOnlyList<ChunkCoord> load, IReadOnlyList<ChunkCoord> unload)
        {
            Load = load;
            Unload = unload;
        }

        public IReadOnlyList<ChunkCoord> Load { get; }

        public IReadOnlyList<ChunkCoord> Unload { get; }
    }
}
