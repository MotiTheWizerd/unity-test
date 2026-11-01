using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Editing
{
    /// <summary>
    /// The editor's working state: read-only master plugins plus one active plugin
    /// that receives every change, exactly like the Creation Kit's "active file".
    /// </summary>
    public sealed class EditSession
    {
        readonly IReadOnlyList<Plugin> _masters;
        readonly PluginManifest _activeManifest;
        readonly SortedDictionary<RecordId, Record> _records = new SortedDictionary<RecordId, Record>();
        readonly SortedSet<RecordId> _deletions = new SortedSet<RecordId>();

        /// <param name="masters">Plugins the active plugin builds on; sorted automatically.</param>
        /// <param name="active">The plugin that receives edits; may be empty.</param>
        public EditSession(IEnumerable<Plugin> masters, Plugin active)
        {
            if (active == null)
                throw new ArgumentNullException(nameof(active));

            _masters = LoadOrderSorter.Sort(masters ?? Enumerable.Empty<Plugin>());
            _activeManifest = active.Manifest;
            foreach (var record in active.Records)
                _records.Add(record.Id, record);
            foreach (var deletion in active.Deletions)
                _deletions.Add(deletion);

            World = Resolve();
        }

        /// <summary>Raised after every successful edit with the new world.</summary>
        public event Action<ResolvedWorld> WorldChanged;

        public string ActivePluginId => _activeManifest.Id;

        public ResolvedWorld World { get; private set; }

        public bool IsDirty { get; private set; }

        /// <summary>Adds a new record or overrides an existing one in the active plugin.</summary>
        public void Put(Record record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            _records.TryGetValue(record.Id, out var previousRecord);
            var wasDeleted = _deletions.Remove(record.Id);
            _records[record.Id] = record;

            Commit(() =>
            {
                if (previousRecord != null)
                    _records[record.Id] = previousRecord;
                else
                    _records.Remove(record.Id);
                if (wasDeleted)
                    _deletions.Add(record.Id);
            });
        }

        /// <summary>
        /// Removes a record from the world: drops it if the active plugin owns it,
        /// otherwise records a deletion of the master's record.
        /// </summary>
        public void Delete(RecordId id)
        {
            if (!World.Records.Contains(id))
                throw new KeyNotFoundException($"Record '{id}' does not exist.");

            _records.TryGetValue(id, out var previousRecord);
            _records.Remove(id);
            var ownedByActive = id.Namespace == ActivePluginId;
            var addedDeletion = !ownedByActive && _deletions.Add(id);

            Commit(() =>
            {
                if (previousRecord != null)
                    _records[id] = previousRecord;
                if (addedDeletion)
                    _deletions.Remove(id);
            });
        }

        /// <summary>Returns an unused id in the active plugin, e.g. "user:ref/000042".</summary>
        public RecordId NewId(string prefix)
        {
            if (!Identifiers.IsValidName(prefix))
                throw new ArgumentException($"Invalid id prefix '{prefix}'.", nameof(prefix));

            var stem = prefix + "/";
            var next = _records.Keys
                .Where(id => id.Namespace == ActivePluginId && id.Name.StartsWith(stem, StringComparison.Ordinal))
                .Select(id => int.TryParse(id.Name.Substring(stem.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            return new RecordId(ActivePluginId, stem + next.ToString("D6", CultureInfo.InvariantCulture));
        }

        /// <summary>Snapshot of the active plugin, ready to save.</summary>
        public Plugin BuildActivePlugin() => new Plugin(_activeManifest, _records.Values, _deletions);

        public void MarkSaved() => IsDirty = false;

        // Validates the change by rebuilding the world; on failure restores the
        // previous state so the session never holds invalid data.
        void Commit(Action rollback)
        {
            ResolvedWorld world;
            try
            {
                world = Resolve();
            }
            catch
            {
                rollback();
                throw;
            }

            World = world;
            IsDirty = true;
            WorldChanged?.Invoke(world);
        }

        // SHORTCUT: re-resolves the whole load order on every edit. Fine at
        // prototype scale; replace with incremental resolution for large worlds.
        ResolvedWorld Resolve()
        {
            var loadOrder = new List<Plugin>(_masters) { BuildActivePlugin() };
            return LoadOrderResolver.Resolve(loadOrder);
        }
    }
}
