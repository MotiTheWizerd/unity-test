using System.Collections.Generic;
using System.Linq;
using Forge.Data;

namespace Forge.Editing.History
{
    /// <summary>
    /// Record changes collected over one or more edits, one per record id in first-touched order. Each keeps
    /// the state before its first change and after its latest, so the set always goes straight from where
    /// the records started to where they ended.
    /// </summary>
    sealed class ChangeSet
    {
        readonly List<RecordChange> _ordered = new List<RecordChange>();
        readonly Dictionary<RecordId, RecordChange> _byId = new Dictionary<RecordId, RecordChange>();

        public IReadOnlyList<RecordChange> All => _ordered;

        public void Track(RecordId id, EntryState before, EntryState after)
        {
            if (_byId.TryGetValue(id, out var existing))
            {
                existing.After = after;
                return;
            }

            var change = new RecordChange(id, before, after);
            _ordered.Add(change);
            _byId.Add(id, change);
        }

        public void TrackAll(IEnumerable<RecordChange> changes)
        {
            foreach (var change in changes)
                Track(change.Id, change.Before, change.After);
        }

        /// <summary>The changes that don't end where they started (unlike, say, a record put and deleted again).</summary>
        public List<RecordChange> Effective() => _ordered.Where(c => !c.Before.Equals(c.After)).ToList();

        public void Clear()
        {
            _ordered.Clear();
            _byId.Clear();
        }
    }
}
