using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Forge.World.Assets
{
    /// <summary>
    /// A content table: which Unity asset presents each asset key that records use (e.g. "nature/tree_pine",
    /// "ground/grass"). Data only. Swapping an asset here restyles everything using that key without touching any
    /// plugin. Entries are kept sorted by key, so the asset diffs cleanly.
    /// </summary>
    /// <remarks>
    /// SHORTCUT: entries reference assets directly, so all content loads with the catalog at startup.
    /// Remove when content outgrows memory: load by key through Addressables instead (see ARCHITECTURE.md).
    /// </remarks>
    public abstract class KeyedAssetCatalog<TAsset> : ScriptableObject where TAsset : Object
    {
        [SerializeField] List<Entry> _entries = new List<Entry>();

        [NonSerialized] Dictionary<string, TAsset> _byKey;

        public int Count => _entries.Count;

        public bool TryGet(string assetKey, out TAsset asset)
        {
            asset = null;
            return assetKey != null && Lookup().TryGetValue(assetKey, out asset) && asset != null;
        }

        /// <summary>Adds or replaces the asset for <paramref name="assetKey"/> (used by content tools).</summary>
        public void Set(string assetKey, TAsset asset)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));

            var index = _entries.FindIndex(e => e.Key == assetKey);
            if (index >= 0)
                _entries[index] = new Entry(assetKey, asset);
            else
                _entries.Add(new Entry(assetKey, asset));

            _entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            _byKey = null;
        }

        Dictionary<string, TAsset> Lookup()
        {
            if (_byKey != null)
                return _byKey;

            _byKey = new Dictionary<string, TAsset>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;
                if (_byKey.ContainsKey(entry.Key))
                    Debug.LogWarning($"[Forge] Catalog '{name}' lists '{entry.Key}' more than once; the first entry wins.", this);
                else
                    _byKey.Add(entry.Key, entry.Asset);
            }

            return _byKey;
        }

        // Edits in the Inspector invalidate the lookup.
        protected virtual void OnValidate() => _byKey = null;

        [Serializable]
        struct Entry
        {
            [SerializeField] string _key;

            // The prefab catalog called it _prefab before the catalogs shared this base.
            [SerializeField, FormerlySerializedAs("_prefab")] TAsset _asset;

            public Entry(string key, TAsset asset)
            {
                _key = key;
                _asset = asset;
            }

            public string Key => _key;

            public TAsset Asset => _asset;
        }
    }
}
