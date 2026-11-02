using System;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Presents asset keys with the prefabs of a <see cref="PrefabCatalog"/>; keys it doesn't
    /// list go to the fallback catalog (e.g. primitives, which also flag unknown keys).
    /// </summary>
    public sealed class PrefabAssetCatalog : IAssetCatalog
    {
        readonly PrefabCatalog _prefabs;
        readonly IAssetCatalog _fallback;

        public PrefabAssetCatalog(PrefabCatalog prefabs, IAssetCatalog fallback)
        {
            _prefabs = prefabs != null ? prefabs : throw new ArgumentNullException(nameof(prefabs));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        public GameObject Instantiate(string assetKey, Transform parent) =>
            _prefabs.TryGet(assetKey, out var prefab)
                ? UnityEngine.Object.Instantiate(prefab, parent, false)
                : _fallback.Instantiate(assetKey, parent);
    }
}
