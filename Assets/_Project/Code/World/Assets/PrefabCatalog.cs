using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Which prefab presents each asset key that base objects use (e.g. "nature/tree_pine").
    /// <see cref="PrefabAssetCatalog"/> instantiates from it. Swapping a prefab here re-skins every object using that
    /// key without touching any plugin.
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Prefab Catalog", fileName = "PrefabCatalog")]
    public sealed class PrefabCatalog : KeyedAssetCatalog<GameObject>
    {
    }
}
