using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>Immutable record store produced by resolving a load order.</summary>
    public sealed class RecordStore : IRecordStore
    {
        readonly IReadOnlyDictionary<RecordId, Entry> _entries;

        // Typed views, built on first use. Safe to cache because the store never changes.
        readonly Dictionary<Type, object> _byType = new Dictionary<Type, object>();

        internal RecordStore(IReadOnlyDictionary<RecordId, Entry> entries)
        {
            _entries = entries;
        }

        public int Count => _entries.Count;

        public IEnumerable<Record> All => _entries.Values.Select(e => e.Record);

        public bool Contains(RecordId id) => _entries.ContainsKey(id);

        public bool TryGet(RecordId id, out Record record)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                record = entry.Record;
                return true;
            }

            record = null;
            return false;
        }

        public T Get<T>(RecordId id) where T : Record => RecordLookup.Get<T>(this, id);

        public IEnumerable<T> OfType<T>() where T : Record
        {
            if (!_byType.TryGetValue(typeof(T), out var typed))
                _byType.Add(typeof(T), typed = All.OfType<T>().ToArray());
            return (T[])typed;
        }

        public string GetSource(RecordId id) =>
            _entries.TryGetValue(id, out var entry)
                ? entry.SourcePluginId
                : throw RecordLookup.Missing(id);

        internal readonly struct Entry
        {
            public Entry(Record record, string sourcePluginId)
            {
                Record = record;
                SourcePluginId = sourcePluginId;
            }

            public Record Record { get; }

            public string SourcePluginId { get; }
        }
    }
}
