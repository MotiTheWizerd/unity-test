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

        /// <summary>
        /// The original the instances of <paramref name="assetKey"/> are made from (its prefab), for what makes its own
        /// from it (a character standing on a body of its own); false when it has none (a primitive, an unknown key).
        /// </summary>
        bool TryGetTemplate(string assetKey, out GameObject template);
    }
}
