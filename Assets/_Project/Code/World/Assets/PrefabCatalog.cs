using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Content table: which prefab presents each asset key that records use
    /// (e.g. "nature/tree_pine"). Data only; <see cref="PrefabAssetCatalog"/> instantiates from it.
    /// Swapping a prefab here re-skins every object using that key without touching any plugin.
    /// </summary>
    /// <remarks>
    /// SHORTCUT: entries reference prefabs directly, so all content loads with the catalog at startup.
    /// Remove when content outgrows memory: load by key through Addressables instead (see ARCHITECTURE.md).
    /// </remarks>
    [CreateAssetMenu(menuName = "Forge/Prefab Catalog", fileName = "PrefabCatalog")]
    public sealed class PrefabCatalog : ScriptableObject
    {
        [SerializeField] List<Entry> _entries = new List<Entry>();

        [NonSerialized] Dictionary<string, GameObject> _byKey;

        public int Count => _entries.Count;

        public bool TryGet(string assetKey, out GameObject prefab)
        {
            prefab = null;
            return assetKey != null && Lookup().TryGetValue(assetKey, out prefab) && prefab != null;
        }

        /// <summary>Adds or replaces the prefab for <paramref name="assetKey"/> (used by content tools).</summary>
        public void Set(string assetKey, GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));

            var index = _entries.FindIndex(e => e.Key == assetKey);
            if (index >= 0)
                _entries[index] = new Entry(assetKey, prefab);
            else
                _entries.Add(new Entry(assetKey, prefab));

            _entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            _byKey = null;
        }

        Dictionary<string, GameObject> Lookup()
        {
            if (_byKey != null)
                return _byKey;

            _byKey = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;
                if (_byKey.ContainsKey(entry.Key))
                    Debug.LogWarning($"[Forge] Prefab catalog '{name}' lists '{entry.Key}' more than once; the first entry wins.", this);
                else
                    _byKey.Add(entry.Key, entry.Prefab);
            }

            return _byKey;
        }

        // Edits in the Inspector invalidate the lookup.
        void OnValidate() => _byKey = null;

        [Serializable]
        struct Entry
        {
            [SerializeField] string _key;
            [SerializeField] GameObject _prefab;

            public Entry(string key, GameObject prefab)
            {
                _key = key;
                _prefab = prefab;
            }

            public string Key => _key;

            public GameObject Prefab => _prefab;
        }
    }
}
