using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Which Unity material presents each asset key that material records use (e.g. "material/oakplanks"). Built by
    /// Forge → Materials → Build Materials from the texture folders under Content/Materials.
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Material Catalog", fileName = "MaterialCatalog")]
    public sealed class MaterialCatalog : KeyedAssetCatalog<Material>
    {
    }
}
