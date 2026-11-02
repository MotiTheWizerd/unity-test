using System;
using System.Collections.Generic;
using Forge.Editing.History;

namespace Forge.Editing
{
    /// <summary>
    /// Edits held for review before they enter the undo history. The world shows each one as it is made;
    /// accepting turns the whole draft into a single undo step, discarding rolls all of it back. For work that
    /// is judged as one result, like the world-building agent's. Opened with <see cref="EditSession.BeginDraft"/>,
    /// one at a time; undo, redo and marking the session saved wait until it is closed.
    /// </summary>
    public sealed class EditDraft
    {
        readonly EditSession _session;
        readonly ChangeSet _collected = new ChangeSet();
        IReadOnlyList<RecordChange> _changes = Array.Empty<RecordChange>();
        int _changesRevision;

        internal EditDraft(EditSession session) => _session = session;

        /// <summary>Whether this is the session's open draft; accepting or discarding closes it.</summary>
        public bool IsOpen => ReferenceEquals(_session.Draft, this);

        /// <summary>Goes up with every edit the draft collects, so viewers can tell when it changed.</summary>
        public int Revision { get; private set; }

        /// <summary>What the draft changes, from the state before it to now: one entry per record, no-ops left out.</summary>
        public IReadOnlyList<RecordChange> Changes
        {
            get
            {
                if (_changesRevision != Revision)
                {
                    _changes = _collected.Effective();
                    _changesRevision = Revision;
                }

                return _changes;
            }
        }

        public bool HasChanges => Changes.Count > 0;

        /// <summary>Keeps the draft as one undo step described by <paramref name="description"/>.</summary>
        public void Accept(string description) => _session.AcceptDraft(this, description);

        /// <summary>Rolls every change of the draft back; nothing enters the undo history.</summary>
        public void Discard() => _session.DiscardDraft(this);

        /// <summary>Every change collected, including ones that ended where they started (for rolling back).</summary>
        internal IReadOnlyList<RecordChange> Collected => _collected.All;

        internal void Collect(IEnumerable<RecordChange> changes)
        {
            _collected.TrackAll(changes);
            Revision++;
        }
    }
}
