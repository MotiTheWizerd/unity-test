using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// A load order resolved once, onto which a changing top plugin (the editor's
    /// active plugin) is layered. Masters don't change while editing, so each
    /// <see cref="Overlay"/> costs time proportional to the top plugin, not the world.
    /// </summary>
    public sealed class MasterLayer
    {
        readonly IReadOnlyList<Plugin> _plugins;
        readonly RecordStore _store;
        readonly IReadOnlyDictionary<RecordId, string> _lastToucher;

        MasterLayer(IReadOnlyList<Plugin> plugins, RecordStore store, IReadOnlyDictionary<RecordId, string> lastToucher, ResolvedWorld world)
        {
            _plugins = plugins;
            _store = store;
            _lastToucher = lastToucher;
            World = world;
        }

        /// <summary>The masters on their own, without any top plugin.</summary>
        public ResolvedWorld World { get; }

        /// <summary>Resolves <paramref name="loadOrder"/> (already sorted) with the v1 rules; see <see cref="LoadOrderResolver"/>.</summary>
        public static MasterLayer Build(IReadOnlyList<Plugin> loadOrder)
        {
            LoadOrderResolver.ValidateLoadOrder(loadOrder);

            var entries = new Dictionary<RecordId, RecordStore.Entry>();
            var lastToucher = new Dictionary<RecordId, string>();
            var conflicts = new List<RecordConflict>();
            var warnings = new List<string>();

            foreach (var plugin in loadOrder)
            {
                foreach (var record in plugin.Records)
                {
                    lastToucher.TryGetValue(record.Id, out var previous);
                    ResolutionRules.Write(plugin, record.Id, entries.ContainsKey(record.Id), previous, conflicts, warnings);
                    lastToucher[record.Id] = plugin.Id;
                    entries[record.Id] = new RecordStore.Entry(record, plugin.Id);
                }

                foreach (var deletion in plugin.Deletions)
                {
                    lastToucher.TryGetValue(deletion, out var previous);
                    ResolutionRules.Delete(plugin, deletion, entries.ContainsKey(deletion), previous, conflicts, warnings);
                    lastToucher[deletion] = plugin.Id;
                    entries.Remove(deletion);
                }
            }

            var store = new RecordStore(entries);
            var world = new ResolvedWorld(loadOrder.Select(p => p.Id).ToArray(), store, conflicts, warnings);
            return new MasterLayer(loadOrder.ToArray(), store, lastToucher, world);
        }

        /// <summary>
        /// The world with <paramref name="top"/> loaded last. Gives exactly what
        /// resolving the full load order would, without re-reading the masters.
        /// </summary>
        public ResolvedWorld Overlay(Plugin top)
        {
            if (top == null)
                throw new ArgumentNullException(nameof(top));
            LoadOrderResolver.ValidateLoadOrder(_plugins.Append(top).ToArray());

            var conflicts = new List<RecordConflict>(World.Conflicts);
            var warnings = new List<string>(World.Warnings);

            var written = new Dictionary<RecordId, Record>(top.Records.Count);
            foreach (var record in top.Records)
            {
                ResolutionRules.Write(top, record.Id, _store.Contains(record.Id), PreviousToucher(record.Id), conflicts, warnings);
                written.Add(record.Id, record);
            }

            var deleted = new HashSet<RecordId>();
            foreach (var deletion in top.Deletions)
            {
                ResolutionRules.Delete(top, deletion, _store.Contains(deletion), PreviousToucher(deletion), conflicts, warnings);
                deleted.Add(deletion);
            }

            var loadOrder = World.LoadOrder.Append(top.Id).ToArray();
            return new ResolvedWorld(loadOrder, new LayeredRecordStore(_store, top.Id, written, deleted), conflicts, warnings);
        }

        string PreviousToucher(RecordId id) => _lastToucher.TryGetValue(id, out var plugin) ? plugin : null;
    }
}
