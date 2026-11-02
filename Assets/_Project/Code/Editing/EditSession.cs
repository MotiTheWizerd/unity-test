using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing.History;

namespace Forge.Editing
{
    /// <summary>
    /// The editor's working state: read-only master plugins plus one active plugin
    /// that receives every change, exactly like the Creation Kit's "active file".
    /// Every edit is validated, recorded as an undoable <see cref="EditStep"/>, and
    /// rolled back entirely if it would leave the world invalid.
    /// </summary>
    public sealed class EditSession
    {
        readonly MasterLayer _masters;
        readonly PluginManifest _activeManifest;
        readonly SortedDictionary<RecordId, Record> _records = new SortedDictionary<RecordId, Record>();
        readonly SortedSet<RecordId> _deletions = new SortedSet<RecordId>();
        readonly UndoHistory _history;

        // Changes of the batch in progress, in first-touched order, one per record id.
        readonly List<RecordChange> _pending = new List<RecordChange>();
        readonly Dictionary<RecordId, RecordChange> _pendingById = new Dictionary<RecordId, RecordChange>();
        string _pendingDescription;
        int _batchDepth;

        int _nextStepId = 1;
        int _savedStateId;

        /// <param name="masters">Plugins the active plugin builds on; sorted automatically.</param>
        /// <param name="active">The plugin that receives edits; may be empty.</param>
        /// <param name="historyCapacity">Maximum number of undoable steps kept.</param>
        public EditSession(IEnumerable<Plugin> masters, Plugin active, int historyCapacity = UndoHistory.DefaultCapacity)
        {
            if (active == null)
                throw new ArgumentNullException(nameof(active));

            _masters = MasterLayer.Build(LoadOrderSorter.Sort(masters ?? Enumerable.Empty<Plugin>()));
            _activeManifest = active.Manifest;
            _history = new UndoHistory(historyCapacity);
            foreach (var record in active.Records)
                _records.Add(record.Id, record);
            foreach (var deletion in active.Deletions)
                _deletions.Add(deletion);

            World = Resolve();
        }

        /// <summary>Raised after every edit, undo and redo with the new world.</summary>
        public event Action<ResolvedWorld> WorldChanged;

        public string ActivePluginId => _activeManifest.Id;

        public ResolvedWorld World { get; private set; }

        /// <summary>True when the active plugin differs from what was last saved (or loaded).</summary>
        public bool IsDirty => _history.CurrentStateId != _savedStateId;

        public bool CanUndo => _history.CanUndo;

        public bool CanRedo => _history.CanRedo;

        public string UndoDescription => _history.UndoDescription;

        public string RedoDescription => _history.RedoDescription;

        /// <summary>Adds a new record or overrides an existing one in the active plugin.</summary>
        public void Put(Record record, string description = null)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            Batch(description ?? $"Edit {record.Id}", () => SetEntry(record.Id, EntryState.Written(record)));
        }

        /// <summary>
        /// Removes a record from the world: drops it if the active plugin owns it,
        /// otherwise records a deletion of the master's record.
        /// </summary>
        public void Delete(RecordId id, string description = null)
        {
            Batch(description ?? $"Delete {id}", () =>
            {
                if (!World.Records.Contains(id) && GetEntry(id).Kind != EntryKind.Written)
                    throw new KeyNotFoundException($"Record '{id}' does not exist.");

                SetEntry(id, id.Namespace == ActivePluginId ? EntryState.Absent : EntryState.Deleted);
            });
        }

        /// <summary>
        /// Groups every Put/Delete made inside <paramref name="edits"/> into one
        /// undo step with a single validation. Batches may nest; the outermost wins.
        /// If anything fails, all changes of the batch are rolled back.
        /// </summary>
        public void Batch(string description, Action edits)
        {
            if (edits == null)
                throw new ArgumentNullException(nameof(edits));

            if (_batchDepth == 0)
                _pendingDescription = description;

            _batchDepth++;
            try
            {
                edits();
            }
            catch
            {
                if (_batchDepth == 1)
                    DiscardPending();
                throw;
            }
            finally
            {
                _batchDepth--;
            }

            if (_batchDepth == 0)
                CommitPending();
        }

        /// <summary>Reverts the last step. Returns false when there is nothing to undo.</summary>
        public bool Undo()
        {
            if (_batchDepth > 0)
                throw new InvalidOperationException("Cannot undo while a batch is in progress.");
            if (!_history.CanUndo)
                return false;

            var step = _history.TakeUndo();
            ApplyStates(step, before: true);
            _history.PushRedo(step);
            Publish(Resolve());
            return true;
        }

        /// <summary>Re-applies the last undone step. Returns false when there is nothing to redo.</summary>
        public bool Redo()
        {
            if (_batchDepth > 0)
                throw new InvalidOperationException("Cannot redo while a batch is in progress.");
            if (!_history.CanRedo)
                return false;

            var step = _history.TakeRedo();
            ApplyStates(step, before: false);
            _history.PushUndo(step);
            Publish(Resolve());
            return true;
        }

        /// <summary>Returns an unused id in the active plugin, e.g. "user:ref/000042".</summary>
        public RecordId NewId(string prefix) => NewIds(prefix, 1)[0];

        /// <summary>
        /// <paramref name="count"/> consecutive unused ids (e.g. for a row of objects created in one
        /// batch). Calling <see cref="NewId"/> repeatedly before putting the records would return
        /// the same id each time, because ids are free until a record uses them.
        /// </summary>
        public IReadOnlyList<RecordId> NewIds(string prefix, int count)
        {
            if (!Identifiers.IsValidName(prefix))
                throw new ArgumentException($"Invalid id prefix '{prefix}'.", nameof(prefix));
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), count, "At least one id must be requested.");

            var stem = prefix + "/";
            var first = _records.Keys
                .Where(id => id.Namespace == ActivePluginId && id.Name.StartsWith(stem, StringComparison.Ordinal))
                .Select(id => int.TryParse(id.Name.Substring(stem.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            var ids = new RecordId[count];
            for (var i = 0; i < count; i++)
                ids[i] = new RecordId(ActivePluginId, stem + (first + i).ToString("D6", CultureInfo.InvariantCulture));
            return ids;
        }

        /// <summary>Snapshot of the active plugin, ready to save.</summary>
        public Plugin BuildActivePlugin() => new Plugin(_activeManifest, _records.Values, _deletions);

        /// <summary>Marks the current state as saved; undoing or redoing away from it makes the session dirty again.</summary>
        public void MarkSaved() => _savedStateId = _history.CurrentStateId;

        EntryState GetEntry(RecordId id)
        {
            if (_records.TryGetValue(id, out var record))
                return EntryState.Written(record);
            return _deletions.Contains(id) ? EntryState.Deleted : EntryState.Absent;
        }

        // Applies a change immediately and remembers the earliest "before" for the batch.
        void SetEntry(RecordId id, EntryState after)
        {
            var before = GetEntry(id);
            if (before.Equals(after))
                return;

            if (_pendingById.TryGetValue(id, out var existing))
            {
                existing.After = after;
            }
            else
            {
                var change = new RecordChange(id, before, after);
                _pending.Add(change);
                _pendingById.Add(id, change);
            }

            ApplyState(id, after);
        }

        void ApplyState(RecordId id, EntryState state)
        {
            switch (state.Kind)
            {
                case EntryKind.Absent:
                    _records.Remove(id);
                    _deletions.Remove(id);
                    break;
                case EntryKind.Written:
                    _records[id] = state.Record;
                    _deletions.Remove(id);
                    break;
                case EntryKind.Deleted:
                    _records.Remove(id);
                    _deletions.Add(id);
                    break;
            }
        }

        void ApplyStates(EditStep step, bool before)
        {
            var changes = step.Changes;
            if (before)
            {
                for (var i = changes.Count - 1; i >= 0; i--)
                    ApplyState(changes[i].Id, changes[i].Before);
            }
            else
            {
                foreach (var change in changes)
                    ApplyState(change.Id, change.After);
            }
        }

        void CommitPending()
        {
            // Changes that ended where they started (e.g. put then delete) are no-ops.
            var changes = _pending.Where(c => !c.Before.Equals(c.After)).ToList();
            var description = _pendingDescription;
            if (changes.Count == 0)
            {
                ClearPending();
                return;
            }

            ResolvedWorld world;
            try
            {
                world = Resolve();
            }
            catch
            {
                DiscardPending();
                throw;
            }

            ClearPending();
            _history.Record(new EditStep(_nextStepId++, description, changes));
            Publish(world);
        }

        void DiscardPending()
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
                ApplyState(_pending[i].Id, _pending[i].Before);
            ClearPending();
        }

        void ClearPending()
        {
            _pending.Clear();
            _pendingById.Clear();
            _pendingDescription = null;
        }

        void Publish(ResolvedWorld world)
        {
            World = world;
            WorldChanged?.Invoke(world);
        }

        // Masters are resolved once; each edit only layers the active plugin over
        // them, so its cost follows the size of the active plugin, not the world.
        ResolvedWorld Resolve() => _masters.Overlay(BuildActivePlugin());
    }
}
