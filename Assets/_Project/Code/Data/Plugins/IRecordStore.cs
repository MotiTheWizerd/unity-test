using System.Collections.Generic;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// Read-only view of resolved world data, shared by the game and the editor. Placed objects are kept a chunk at
    /// a time and read when a chunk is first asked for (<see cref="ObjectsIn"/>), so prefer the chunk-scoped members
    /// for them: <see cref="All"/> and <see cref="OfType{T}"/> read every chunk.
    /// </summary>
    public interface IRecordStore
    {
        /// <summary>How many records the world has, placed objects included; known without reading them.</summary>
        int Count { get; }

        /// <summary>Every record. Reads every chunk's objects.</summary>
        IEnumerable<Record> All { get; }

        bool Contains(RecordId id);

        bool TryGet(RecordId id, out Record record);

        /// <summary>Returns the record as <typeparamref name="T"/>; throws if missing or of another type.</summary>
        T Get<T>(RecordId id) where T : Record;

        /// <summary>Records of a type. For placed objects this reads every chunk; prefer <see cref="ObjectsIn"/>.</summary>
        IEnumerable<T> OfType<T>() where T : Record;

        /// <summary>Id of the plugin whose version of the record won.</summary>
        string GetSource(RecordId id);

        /// <summary>The chunks of <paramref name="space"/> that hold placed objects, known without reading them.</summary>
        IReadOnlyList<ChunkCoord> ObjectChunks(RecordId space);

        /// <summary>The placed objects standing in chunk <paramref name="coord"/> of <paramref name="space"/>, sorted by id.</summary>
        IReadOnlyList<PlacedObjectRecord> ObjectsIn(RecordId space, ChunkCoord coord);
    }
}
