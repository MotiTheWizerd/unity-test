using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Which terrain layer (textures and tiling) presents each asset key that ground layer records use (e.g.
    /// "ground/grass"), and the one unpainted ground shows. Built by Forge → Ground → Build Ground Layers from the
    /// texture folders under Content/Ground.
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Ground Layer Catalog", fileName = "GroundLayerCatalog")]
    public sealed class GroundLayerCatalog : KeyedAssetCatalog<TerrainLayer>
    {
        [SerializeField, Tooltip("Asset key of the layer unpainted ground shows, and any layer the catalog lacks.")]
        string _baseKey = "ground/grass";

        /// <summary>Asset key of the layer unpainted ground shows.</summary>
        public string BaseKey => _baseKey;

        /// <summary>The layer unpainted ground shows; null when the catalog has none for <see cref="BaseKey"/>.</summary>
        public TerrainLayer Base => TryGet(_baseKey, out var layer) ? layer : null;
    }
}
