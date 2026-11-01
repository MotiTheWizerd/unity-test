using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>Read-only view of resolved world data, shared by the game and the editor.</summary>
    public interface IRecordStore
    {
        int Count { get; }

        IEnumerable<Record> All { get; }

        bool Contains(RecordId id);

        bool TryGet(RecordId id, out Record record);

        /// <summary>Returns the record as <typeparamref name="T"/>; throws if missing or of another type.</summary>
        T Get<T>(RecordId id) where T : Record;

        IEnumerable<T> OfType<T>() where T : Record;

        /// <summary>Id of the plugin whose version of the record won.</summary>
        string GetSource(RecordId id);
    }
}
