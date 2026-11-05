using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// A resolved master store with one top plugin's records and deletions
    /// layered over it. Lookups check the top layer first. Building one costs
    /// time proportional to the top plugin; the master store is shared, not copied.
    /// </summary>
    sealed class LayeredRecordStore : IRecordStore
    {
        static readonly IReadOnlyList<PlacedObjectRecord> None = Array.Empty<PlacedObjectRecord>();

        readonly RecordStore _base;
        readonly string _topPluginId;
        readonly IReadOnlyDictionary<RecordId, Record> _written;
        readonly HashSet<RecordId> _deleted;

        // The top plugin's objects by block, and each block as seen through the layer, both built on first use.
        Dictionary<ObjectBlockKey, List<PlacedObjectRecord>> _writtenObjects;
        readonly Dictionary<ObjectBlockKey, IReadOnlyList<PlacedObjectRecord>> _layered = new Dictionary<ObjectBlockKey, IReadOnlyList<PlacedObjectRecord>>();

        public LayeredRecordStore(RecordStore baseStore, string topPluginId, IReadOnlyDictionary<RecordId, Record> written, HashSet<RecordId> deleted)
        {
            _base = baseStore;
            _topPluginId = topPluginId;
            _written = written;
            _deleted = deleted;
            Count = baseStore.Count
                    + written.Keys.Count(id => !baseStore.Contains(id))
                    - deleted.Count(baseStore.Contains);
        }

        public int Count { get; }

        public IEnumerable<Record> All =>
            _base.All.Where(r => !IsShadowed(r.Id)).Concat(_written.Values);

        public bool Contains(RecordId id) =>
            _written.ContainsKey(id) || (!_deleted.Contains(id) && _base.Contains(id));

        public bool TryGet(RecordId id, out Record record)
        {
            if (_written.TryGetValue(id, out record))
                return true;
            if (!_deleted.Contains(id))
                return _base.TryGet(id, out record);

            record = null;
            return false;
        }

        public T Get<T>(RecordId id) where T : Record => RecordLookup.Get<T>(this, id);

        public IEnumerable<T> OfType<T>() where T : Record =>
            _base.OfType<T>().Where(r => !IsShadowed(r.Id)).Concat(_written.Values.OfType<T>());

        public string GetSource(RecordId id)
        {
            if (_written.ContainsKey(id))
                return _topPluginId;
            if (_deleted.Contains(id))
                throw RecordLookup.Missing(id);
            return _base.GetSource(id);
        }

        public IReadOnlyList<ChunkCoord> ObjectChunks(RecordId space)
        {
            var chunks = _base.ObjectChunks(space);
            var written = WrittenObjects().Keys.Where(k => k.Space == space).Select(k => k.Coord);
            return written.Any() ? chunks.Union(written).ToArray() : chunks;
        }

        public IReadOnlyList<PlacedObjectRecord> ObjectsIn(RecordId space, ChunkCoord coord)
        {
            var key = new ObjectBlockKey(space, coord);
            if (_layered.TryGetValue(key, out var objects))
                return objects;

            var below = _base.ObjectsIn(space, coord);
            WrittenObjects().TryGetValue(key, out var written);
            if (written == null && !below.Any(p => IsShadowed(p.Id)))
                objects = below;
            else
                objects = below.Where(p => !IsShadowed(p.Id)).Concat(written ?? Enumerable.Empty<PlacedObjectRecord>()).OrderBy(p => p.Id).ToArray();
            if (objects.Count == 0)
                objects = None;

            _layered.Add(key, objects);
            return objects;
        }

        /// <summary>
        /// Ids whose record differs from <paramref name="earlier"/>, when both layer
        /// over the same master store; null when they don't and the difference is unknown.
        /// </summary>
        public IReadOnlyCollection<RecordId> ChangedSince(LayeredRecordStore earlier)
        {
            if (earlier == null || !ReferenceEquals(_base, earlier._base))
                return null;

            var changed = new HashSet<RecordId>();
            AddDifferences(this, earlier, changed);
            AddDifferences(earlier, this, changed);
            return changed;
        }

        // Records are immutable, so a different instance (or a different
        // deleted flag) under the same id means the record changed.
        static void AddDifferences(LayeredRecordStore a, LayeredRecordStore b, HashSet<RecordId> changed)
        {
            foreach (var pair in a._written)
            {
                if (!b._written.TryGetValue(pair.Key, out var other) || !ReferenceEquals(pair.Value, other))
                    changed.Add(pair.Key);
            }

            foreach (var id in a._deleted)
            {
                if (!b._deleted.Contains(id))
                    changed.Add(id);
            }
        }

        Dictionary<ObjectBlockKey, List<PlacedObjectRecord>> WrittenObjects()
        {
            if (_writtenObjects != null)
                return _writtenObjects;

            var grouped = new Dictionary<ObjectBlockKey, List<PlacedObjectRecord>>();
            foreach (var placed in _written.Values.OfType<PlacedObjectRecord>())
            {
                var key = ObjectBlockKey.Of(placed);
                if (!grouped.TryGetValue(key, out var list))
                    grouped.Add(key, list = new List<PlacedObjectRecord>());
                list.Add(placed);
            }

            return _writtenObjects = grouped;
        }

        bool IsShadowed(RecordId id) => _written.ContainsKey(id) || _deleted.Contains(id);
    }
}
