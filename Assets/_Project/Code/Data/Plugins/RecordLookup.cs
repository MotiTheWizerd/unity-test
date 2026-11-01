using System;
using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>Lookup behaviour shared by every <see cref="IRecordStore"/> implementation.</summary>
    static class RecordLookup
    {
        public static T Get<T>(IRecordStore store, RecordId id) where T : Record
        {
            if (!store.TryGet(id, out var record))
                throw Missing(id);
            if (record is T typed)
                return typed;
            throw new InvalidCastException($"Record '{id}' is a {record.GetType().Name}, not a {typeof(T).Name}.");
        }

        public static KeyNotFoundException Missing(RecordId id) => new KeyNotFoundException($"Record '{id}' does not exist.");
    }
}
