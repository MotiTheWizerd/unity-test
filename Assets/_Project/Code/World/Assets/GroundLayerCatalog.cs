using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Which terrain layer (textures and tiling) presents each asset key that ground layer records use (e.g.
    /// "ground/grass"), the one unpainted ground shows, and each layer's map color (its texture's average, what it
    /// looks like from afar: the far land is drawn in it). Built by Forge → Ground → Build Ground Layers from the
    /// texture folders under Content/Ground.
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Ground Layer Catalog", fileName = "GroundLayerCatalog")]
    public sealed class GroundLayerCatalog : KeyedAssetCatalog<TerrainLayer>
    {
        [SerializeField, Tooltip("Asset key of the layer unpainted ground shows, and any layer the catalog lacks.")]
        string _baseKey = "ground/grass";

        [SerializeField, Tooltip("Each layer's average color, linear, for the far land; set by the builder.")]
        List<MapColor> _mapColors = new List<MapColor>();

        [SerializeField, Tooltip("What a layer's material ball is made from (URP Lit with normal, metallic and occlusion maps on); set by the builder. Being an asset keeps those shader variants in a build.")]
        Material _previewMaterial;

        /// <summary>Asset key of the layer unpainted ground shows.</summary>
        public string BaseKey => _baseKey;

        /// <summary>The template a layer's preview material is copied from (<see cref="TerrainLayerPreview"/>); null when unset.</summary>
        public Material PreviewMaterial
        {
            get => _previewMaterial;
            set => _previewMaterial = value;
        }

        /// <summary>The layer unpainted ground shows; null when the catalog has none for <see cref="BaseKey"/>.</summary>
        public TerrainLayer Base => TryGet(_baseKey, out var layer) ? layer : null;

        /// <summary>The map color (linear) of <paramref name="assetKey"/>; false when the catalog has none.</summary>
        public bool TryGetMapColor(string assetKey, out Color color)
        {
            var index = _mapColors.FindIndex(entry => entry.Key == assetKey);
            color = index >= 0 ? _mapColors[index].Color : default;
            return index >= 0;
        }

        /// <summary>Sets the map color (linear) of <paramref name="assetKey"/> (used by the builder).</summary>
        public void SetMapColor(string assetKey, Color color)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
                throw new ArgumentException("Asset key is required.", nameof(assetKey));

            _mapColors.RemoveAll(entry => entry.Key == assetKey);
            _mapColors.Add(new MapColor(assetKey, color));
            _mapColors.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }

        [Serializable]
        struct MapColor
        {
            [SerializeField] string _key;
            [SerializeField] Color _color;

            public MapColor(string key, Color color)
            {
                _key = key;
                _color = color;
            }

            public string Key => _key;

            public Color Color => _color;
        }
    }
}
