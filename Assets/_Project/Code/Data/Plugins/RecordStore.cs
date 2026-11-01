using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>Immutable record store produced by <see cref="LoadOrderResolver"/>.</summary>
    public sealed class RecordStore : IRecordStore
    {
        readonly IReadOnlyDictionary<RecordId, Entry> _entries;

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

        public T Get<T>(RecordId id) where T : Record
        {
            if (!_entries.TryGetValue(id, out var entry))
                throw new KeyNotFoundException($"Record '{id}' does not exist.");
            if (entry.Record is T typed)
                return typed;
            throw new InvalidCastException($"Record '{id}' is a {entry.Record.GetType().Name}, not a {typeof(T).Name}.");
        }

        public IEnumerable<T> OfType<T>() where T : Record => All.OfType<T>();

        public string GetSource(RecordId id) =>
            _entries.TryGetValue(id, out var entry)
                ? entry.SourcePluginId
                : throw new KeyNotFoundException($"Record '{id}' does not exist.");

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
