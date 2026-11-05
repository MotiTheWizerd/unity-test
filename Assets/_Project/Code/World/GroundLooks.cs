using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// How each ground layer looks: a <see cref="GroundLayerRecord"/>'s asset key, found in the
    /// <see cref="GroundLayerCatalog"/>, gives its terrain layer. A layer the world or the catalog lacks shows as the
    /// catalog's base layer, with a warning once, so missing content is visible but never breaks the land.
    /// </summary>
    public sealed class GroundLooks
    {
        readonly GroundLayerCatalog _catalog;
        readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        IRecordStore _records;

        public GroundLooks(GroundLayerCatalog catalog)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            if (_catalog.Base == null)
                Debug.LogWarning($"[Forge] Ground layer catalog '{catalog.name}' has no layer for its base key '{catalog.BaseKey}'; unpainted ground shows no texture.", catalog);
        }

        /// <summary>What unpainted ground shows; null when the catalog has none.</summary>
        public TerrainLayer Base => _catalog.Base;

        /// <summary>The records ground layers are looked up in: the world on show.</summary>
        public void Use(IRecordStore records) => _records = records ?? throw new ArgumentNullException(nameof(records));

        /// <summary>The terrain layer presenting ground layer <paramref name="layer"/>, or the base layer when it can't be found.</summary>
        public TerrainLayer Of(RecordId layer)
        {
            if (_records == null || !(_records.TryGet(layer, out var record) && record is GroundLayerRecord ground))
                return Fallback(layer.ToString(), $"there is no ground layer '{layer}'");
            return _catalog.TryGet(ground.AssetKey, out var look)
                ? look
                : Fallback(ground.AssetKey, $"the ground layer catalog has nothing for '{ground.AssetKey}' ({layer})");
        }

        TerrainLayer Fallback(string what, string problem)
        {
            if (_warned.Add(what))
                Debug.LogWarning($"[Forge] Painted ground shows the base layer where {problem}.");
            return Base;
        }
    }
}
