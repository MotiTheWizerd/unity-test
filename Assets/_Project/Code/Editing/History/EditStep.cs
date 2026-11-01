using System.Collections.Generic;
using Forge.Data;

namespace Forge.Editing.History
{
    /// <summary>How one record id in the active plugin changed during an edit.</summary>
    public sealed class RecordChange
    {
        public RecordChange(RecordId id, EntryState before, EntryState after)
        {
            Id = id;
            Before = before;
            After = after;
        }

        public RecordId Id { get; }

        public EntryState Before { get; }

        public EntryState After { get; internal set; }
    }

    /// <summary>
    /// One undoable unit: every record change made by a single edit or batch.
    /// Undo restores each <see cref="RecordChange.Before"/>, redo each <see cref="RecordChange.After"/>.
    /// </summary>
    public sealed class EditStep
    {
        internal EditStep(int id, string description, IReadOnlyList<RecordChange> changes)
        {
            Id = id;
            Description = description;
            Changes = changes;
        }

        /// <summary>Unique, increasing id; identifies the state reached after this step.</summary>
        public int Id { get; }

        public string Description { get; }

        public IReadOnlyList<RecordChange> Changes { get; }
    }
}
