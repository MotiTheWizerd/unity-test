using System;
using System.Collections.Generic;

namespace Forge.Data.Records
{
    /// <summary>
    /// Explicit, closed mapping between record types and their schema keys
    /// (e.g. "placed_object"). Serializers use it as the only way to turn a key
    /// from a file into a type, so untrusted plugin files can never name
    /// arbitrary CLR types.
    /// </summary>
    public sealed class RecordTypeRegistry
    {
        readonly Dictionary<string, Type> _typesByKey = new Dictionary<string, Type>(StringComparer.Ordinal);
        readonly Dictionary<Type, string> _keysByType = new Dictionary<Type, string>();

        /// <summary>Registry containing every built-in record type.</summary>
        public static RecordTypeRegistry CreateDefault()
        {
            var registry = new RecordTypeRegistry();
            registry.Register<BaseObjectRecord>("base_object");
            registry.Register<PlacedObjectRecord>("placed_object");
            registry.Register<TerrainChunkRecord>("terrain_chunk");
            registry.Register<SpaceRecord>("space");
            registry.Register<LakeRecord>("lake");
            registry.Register<ClimateRecord>("climate");
            registry.Register<GroundLayerRecord>("ground_layer");
            registry.Register<MaterialRecord>("material");
            registry.Register<AttributeRecord>("attribute");
            return registry;
        }

        public IEnumerable<string> Keys => _typesByKey.Keys;

        public void Register<T>(string key) where T : Record
        {
            if (!Identifiers.IsValidKey(key))
                throw new ArgumentException($"Invalid record type key '{key}'.", nameof(key));
            if (_typesByKey.ContainsKey(key))
                throw new ArgumentException($"Record type key '{key}' is already registered.", nameof(key));
            if (_keysByType.ContainsKey(typeof(T)))
                throw new ArgumentException($"Record type {typeof(T).Name} is already registered.");

            _typesByKey.Add(key, typeof(T));
            _keysByType.Add(typeof(T), key);
        }

        public bool TryGetType(string key, out Type type) =>
            _typesByKey.TryGetValue(key ?? string.Empty, out type);

        public string GetKey(Type type)
        {
            if (type != null && _keysByType.TryGetValue(type, out var key))
                return key;
            throw new KeyNotFoundException($"Record type {type?.Name ?? "null"} is not registered.");
        }
    }
}
