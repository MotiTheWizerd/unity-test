using System.Collections.Generic;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Which Unity material presents each asset key that material records use (e.g. "material/oakplanks"). The base
    /// game's is built by Forge → Materials → Build Materials from the texture folders under Content/Materials; each
    /// material pack has its own, built by Build Material Packs into a Resources/<see cref="PacksResourcesFolder"/>
    /// folder, so the editor finds every pack present without a reference to it (<see cref="LoadPacks"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Material Catalog", fileName = "MaterialCatalog")]
    public sealed class MaterialCatalog : KeyedAssetCatalog<Material>
    {
        /// <summary>The Resources folder material packs keep their catalogs in.</summary>
        public const string PacksResourcesFolder = "MaterialPacks";

        /// <summary>The catalogs of every material pack in the build (none when there are no packs).</summary>
        public static IReadOnlyList<MaterialCatalog> LoadPacks() => Resources.LoadAll<MaterialCatalog>(PacksResourcesFolder);
    }
}
