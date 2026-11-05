using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// The placed objects of a resolved load order, a block at a time: a block is folded (last plugin to touch an id
    /// wins, deletions drop it) the first time it is asked for and kept. An object a later plugin moved to another
    /// chunk shows only there. Nothing is read for blocks nobody asks for, so a world's objects stay on disk until the
    /// land they stand on is in use.
    /// </summary>
    sealed class MasterObjects
    {
        static readonly IReadOnlyList<PlacedObjectRecord> None = Array.Empty<PlacedObjectRecord>();

        readonly IReadOnlyList<Plugin> _plugins;
        readonly HashSet<RecordId>[] _deletions;
        readonly Dictionary<ObjectBlockKey, ResolvedBlock> _resolved = new Dictionary<ObjectBlockKey, ResolvedBlock>();
        readonly Dictionary<RecordId, ChunkCoord[]> _chunksBySpace;
        readonly ObjectBlockKey[] _keys;

        /// <param name="plugins">The load order, sorted.</param>
        /// <param name="conflicts">Receives the conflicts among the plugins' objects, as <see cref="ResolutionRules"/> defines them.</param>
        /// <param name="warnings">Receives the warnings among the plugins' objects.</param>
        public MasterObjects(IReadOnlyList<Plugin> plugins, List<RecordConflict> conflicts, List<string> warnings)
        {
            _plugins = plugins;
            _deletions = plugins.Select(p => new HashSet<RecordId>(p.Deletions)).ToArray();

            _keys = plugins.SelectMany(p => p.Objects.Keys).Distinct().OrderBy(k => k).ToArray();
            _chunksBySpace = _keys.GroupBy(k => k.Space).ToDictionary(g => g.Key, g => g.Select(k => k.Coord).ToArray());

            // Every object is counted once per plugin that writes it; each override of a live object and each
            // deletion of one takes one away. The same walk yields the warnings and conflicts, as a full fold would.
            var count = plugins.Sum(p => p.Objects.TotalCount);
            for (var i = 0; i < plugins.Count; i++)
            {
                var plugin = plugins[i];
                foreach (var id in plugin.Objects.Overrides)
                {
                    var previous = LastToucherBefore(id, i);
                    ResolutionRules.Write(plugin, id, previous.IsWriter, previous.PluginId, conflicts, warnings);
                    if (previous.IsWriter)
                        count--;
                }

                foreach (var id in plugin.Deletions)
                {
                    if (!IsObjectId(id, i))
                        continue;
                    var previous = LastToucherBefore(id, i);
                    ResolutionRules.Delete(plugin, id, previous.IsWriter, previous.PluginId, conflicts, warnings);
                    if (previous.IsWriter)
                        count--;
                }
            }

            Count = count;
        }

        /// <summary>How many objects the load order leaves in the world, known without reading a block.</summary>
        public int Count { get; }

        /// <summary>Every block any plugin has objects in, sorted.</summary>
        public IReadOnlyList<ObjectBlockKey> Keys => _keys;

        public IReadOnlyList<ChunkCoord> ChunksOf(RecordId space) =>
            _chunksBySpace.TryGetValue(space, out var chunks) ? chunks : Array.Empty<ChunkCoord>();

        /// <summary>The objects the load order leaves in block <paramref name="key"/>, sorted by id; folded on first use.</summary>
        public IReadOnlyList<PlacedObjectRecord> ObjectsIn(ObjectBlockKey key) => Resolve(key).Records;

        /// <summary>The object <paramref name="id"/> and the plugin whose version won; false if the load order has none.</summary>
        public bool TryGet(RecordId id, out PlacedObjectRecord record, out string source)
        {
            record = null;
            source = null;
            var last = LastToucherBefore(id, _plugins.Count);
            if (!last.IsWriter)
                return false;

            // The winner's block holds it, unless the winner itself was overridden into another chunk (then that
            // plugin is the last toucher instead), so the lookup can't miss.
            var block = Resolve(last.Key);
            if (!block.ById.TryGetValue(id, out var entry))
                return false;
            record = entry.Record;
            source = entry.Source;
            return true;
        }

        public bool Contains(RecordId id) => LastToucherBefore(id, _plugins.Count).IsWriter;

        /// <summary>Whether <paramref name="id"/> names a placed object: some plugin keeps it in a block.</summary>
        public bool IsObject(RecordId id) => IsObjectId(id, _plugins.Count);

        /// <summary>Every object the load order leaves in the world, block by block. Reads every block.</summary>
        public IEnumerable<PlacedObjectRecord> All => _keys.SelectMany(key => Resolve(key).Records);

        /// <summary>Who last wrote or deleted <paramref name="id"/> among the plugins before <paramref name="before"/>, if any.</summary>
        public Toucher LastToucherBefore(RecordId id, int before)
        {
            for (var j = Math.Min(before, _plugins.Count) - 1; j >= 0; j--)
            {
                if (_plugins[j].Objects.TryLocate(id, out var key))
                    return new Toucher(_plugins[j].Id, true, key);
                if (_deletions[j].Contains(id))
                    return new Toucher(_plugins[j].Id, false, default);
            }

            return default;
        }

        // Whether a deletion names an object: some earlier plugin keeps the id in a block.
        bool IsObjectId(RecordId id, int before)
        {
            for (var j = 0; j < before; j++)
            {
                if (_plugins[j].Objects.TryLocate(id, out _))
                    return true;
            }

            return false;
        }

        ResolvedBlock Resolve(ObjectBlockKey key)
        {
            if (_resolved.TryGetValue(key, out var block))
                return block;

            var byId = new Dictionary<RecordId, Entry>();
            for (var i = 0; i < _plugins.Count; i++)
            {
                var plugin = _plugins[i];
                foreach (var placed in plugin.Objects.Read(key))
                {
                    // An id a later plugin writes shows in that plugin's block (which may be another chunk); one a
                    // later plugin deletes shows nowhere.
                    if (!TouchedAfter(placed.Id, i))
                        byId[placed.Id] = new Entry(placed, plugin.Id);
                }

                foreach (var deletion in plugin.Deletions)
                    byId.Remove(deletion);
            }

            block = new ResolvedBlock(byId);
            _resolved.Add(key, block);
            return block;
        }

        bool TouchedAfter(RecordId id, int plugin)
        {
            for (var j = plugin + 1; j < _plugins.Count; j++)
            {
                if (_plugins[j].Objects.TryLocate(id, out _) || _deletions[j].Contains(id))
                    return true;
            }

            return false;
        }

        public readonly struct Toucher
        {
            public Toucher(string pluginId, bool isWriter, ObjectBlockKey key)
            {
                PluginId = pluginId;
                IsWriter = isWriter;
                Key = key;
            }

            /// <summary>The plugin, or null when nobody touched the id.</summary>
            public string PluginId { get; }

            /// <summary>True when the last touch wrote the object (so it is in the world), false when it deleted it.</summary>
            public bool IsWriter { get; }

            /// <summary>The block the writer put it in.</summary>
            public ObjectBlockKey Key { get; }
        }

        readonly struct Entry
        {
            public Entry(PlacedObjectRecord record, string source)
            {
                Record = record;
                Source = source;
            }

            public PlacedObjectRecord Record { get; }

            public string Source { get; }
        }

        sealed class ResolvedBlock
        {
            public ResolvedBlock(Dictionary<RecordId, Entry> byId)
            {
                ById = byId;
                Records = byId.Count == 0 ? None : byId.Values.Select(e => e.Record).OrderBy(r => r.Id).ToArray();
            }

            public Dictionary<RecordId, Entry> ById { get; }

            public IReadOnlyList<PlacedObjectRecord> Records { get; }
        }
    }
}
