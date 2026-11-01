using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// A resolved master store with one top plugin's records and deletions
    /// layered over it. Lookups check the top layer first. Building one costs
    /// time proportional to the top plugin; the master store is shared, not copied.
    /// </summary>
    sealed class LayeredRecordStore : IRecordStore
    {
        readonly RecordStore _base;
        readonly string _topPluginId;
        readonly IReadOnlyDictionary<RecordId, Record> _written;
        readonly HashSet<RecordId> _deleted;

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

        bool IsShadowed(RecordId id) => _written.ContainsKey(id) || _deleted.Contains(id);
    }
}
