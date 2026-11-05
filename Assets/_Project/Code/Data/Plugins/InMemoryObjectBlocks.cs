using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>Placed objects held in memory, grouped by block: a plugin being edited, built by a tool, or made in a test.</summary>
    public sealed class InMemoryObjectBlocks : IObjectBlocks
    {
        static readonly IReadOnlyList<PlacedObjectRecord> None = Array.Empty<PlacedObjectRecord>();

        readonly Dictionary<ObjectBlockKey, PlacedObjectRecord[]> _blocks = new Dictionary<ObjectBlockKey, PlacedObjectRecord[]>();
        readonly Dictionary<RecordId, ObjectBlockKey> _location = new Dictionary<RecordId, ObjectBlockKey>();
        readonly ObjectBlockKey[] _keys;
        readonly RecordId[] _overrides;

        /// <param name="pluginId">The plugin the objects belong to; objects in other namespaces are its overrides.</param>
        public InMemoryObjectBlocks(string pluginId, IEnumerable<PlacedObjectRecord> objects)
        {
            if (string.IsNullOrEmpty(pluginId))
                throw new ArgumentException("The plugin id is required.", nameof(pluginId));
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));

            var grouped = new Dictionary<ObjectBlockKey, List<PlacedObjectRecord>>();
            var overrides = new List<RecordId>();
            foreach (var placed in objects)
            {
                if (placed == null)
                    throw new ArgumentException("Objects can't be missing.", nameof(objects));
                var key = ObjectBlockKey.Of(placed);
                if (!grouped.TryGetValue(key, out var list))
                    grouped.Add(key, list = new List<PlacedObjectRecord>());
                list.Add(placed);
                if (_location.ContainsKey(placed.Id))
                    throw new ArgumentException($"Object '{placed.Id}' is listed more than once.", nameof(objects));
                _location.Add(placed.Id, key);
                if (placed.Id.Namespace != pluginId)
                    overrides.Add(placed.Id);
            }

            foreach (var pair in grouped)
                _blocks.Add(pair.Key, pair.Value.OrderBy(p => p.Id).ToArray());
            _keys = _blocks.Keys.OrderBy(k => k).ToArray();
            _overrides = overrides.OrderBy(id => id).ToArray();
            TotalCount = _location.Count;
        }

        public static InMemoryObjectBlocks Empty(string pluginId) => new InMemoryObjectBlocks(pluginId, Enumerable.Empty<PlacedObjectRecord>());

        public IReadOnlyList<ObjectBlockKey> Keys => _keys;

        public int TotalCount { get; }

        public IReadOnlyCollection<RecordId> Overrides => _overrides;

        public int CountOf(ObjectBlockKey key) => _blocks.TryGetValue(key, out var block) ? block.Length : 0;

        public IReadOnlyList<PlacedObjectRecord> Read(ObjectBlockKey key) => _blocks.TryGetValue(key, out var block) ? block : None;

        public bool TryLocate(RecordId id, out ObjectBlockKey key) => _location.TryGetValue(id, out key);

        /// <summary>Every object, block by block.</summary>
        public IEnumerable<PlacedObjectRecord> All => _keys.SelectMany(key => _blocks[key]);
    }
}
