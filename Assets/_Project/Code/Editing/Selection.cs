using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;

namespace Forge.Editing
{
    /// <summary>The set of records the user has selected, in selection order.</summary>
    public sealed class Selection
    {
        readonly List<RecordId> _ids = new List<RecordId>();

        /// <summary>Raised whenever the set of selected ids changes.</summary>
        public event Action Changed;

        public IReadOnlyList<RecordId> Ids => _ids;

        public int Count => _ids.Count;

        public bool IsEmpty => _ids.Count == 0;

        public bool Contains(RecordId id) => _ids.Contains(id);

        /// <summary>Replaces the selection with a single id.</summary>
        public void Set(RecordId id) => Replace(new[] { id });

        public void Replace(IEnumerable<RecordId> ids)
        {
            var next = (ids ?? Enumerable.Empty<RecordId>()).Where(id => !id.IsDefault).Distinct().ToList();
            if (next.SequenceEqual(_ids))
                return;

            _ids.Clear();
            _ids.AddRange(next);
            Changed?.Invoke();
        }

        /// <summary>Adds the id if absent, removes it if present (shift-click).</summary>
        public void Toggle(RecordId id)
        {
            if (id.IsDefault)
                return;
            if (!_ids.Remove(id))
                _ids.Add(id);
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (_ids.Count == 0)
                return;
            _ids.Clear();
            Changed?.Invoke();
        }

        /// <summary>Drops ids that no longer exist, e.g. after a delete or an undo.</summary>
        public void Prune(IRecordStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (_ids.RemoveAll(id => !store.Contains(id)) > 0)
                Changed?.Invoke();
        }
    }
}
