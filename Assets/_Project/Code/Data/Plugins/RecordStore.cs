using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// Immutable record store produced by resolving a load order: every record but the placed objects in memory,
    /// the placed objects a chunk at a time (<see cref="MasterObjects"/>).
    /// </summary>
    public sealed class RecordStore : IRecordStore
    {
        readonly IReadOnlyDictionary<RecordId, Entry> _entries;
        readonly MasterObjects _objects;

        // Typed views of the records in memory, built on first use. Safe to cache because the store never changes.
        readonly Dictionary<Type, object> _byType = new Dictionary<Type, object>();

        internal RecordStore(IReadOnlyDictionary<RecordId, Entry> entries, MasterObjects objects)
        {
            _entries = entries;
            _objects = objects;
        }

        public int Count => _entries.Count + _objects.Count;

        public IEnumerable<Record> All => _entries.Values.Select(e => e.Record).Concat(_objects.All);

        public bool Contains(RecordId id) => _entries.ContainsKey(id) || _objects.Contains(id);

        public bool TryGet(RecordId id, out Record record)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                record = entry.Record;
                return true;
            }

            if (_objects.TryGet(id, out var placed, out _))
            {
                record = placed;
                return true;
            }

            record = null;
            return false;
        }

        public T Get<T>(RecordId id) where T : Record => RecordLookup.Get<T>(this, id);

        public IEnumerable<T> OfType<T>() where T : Record
        {
            if (!_byType.TryGetValue(typeof(T), out var typed))
                _byType.Add(typeof(T), typed = _entries.Values.Select(e => e.Record).OfType<T>().ToArray());
            var inMemory = (T[])typed;
            // Objects are not held: the typed view reads them each time, so a one-off full scan doesn't pin them.
            return typeof(T).IsAssignableFrom(typeof(PlacedObjectRecord)) ? inMemory.Concat(_objects.All.OfType<T>()) : inMemory;
        }

        public string GetSource(RecordId id)
        {
            if (_entries.TryGetValue(id, out var entry))
                return entry.SourcePluginId;
            if (_objects.TryGet(id, out _, out var source))
                return source;
            throw RecordLookup.Missing(id);
        }

        public IReadOnlyList<ChunkCoord> ObjectChunks(RecordId space) => _objects.ChunksOf(space);

        public IReadOnlyList<PlacedObjectRecord> ObjectsIn(RecordId space, ChunkCoord coord) =>
            _objects.ObjectsIn(new ObjectBlockKey(space, coord));

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
