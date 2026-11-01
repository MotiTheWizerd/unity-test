using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Resolves the engine-agnostic asset keys stored in records into visuals.
    /// </summary>
    public interface IAssetCatalog
    {
        /// <summary>Creates an instance for <paramref name="assetKey"/>, or a visible placeholder if unknown.</summary>
        GameObject Instantiate(string assetKey, Transform parent);
    }
}
