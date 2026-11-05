using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Editing.Terrain
{
    /// <summary>The ground layers a world offers, for pickers and painting.</summary>
    public static class GroundLayers
    {
        /// <summary>Every ground layer of the world, by display name (then id, so the order is stable).</summary>
        public static IReadOnlyList<GroundLayerRecord> Of(IRecordStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            return store.OfType<GroundLayerRecord>()
                .OrderBy(layer => layer.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(layer => layer.Id)
                .ToList();
        }

        /// <summary>
        /// The ground layer that shows as <paramref name="assetKey"/> (the lowest id when several do), or null:
        /// how unpainted ground, which shows a look, is named once it is painted.
        /// </summary>
        public static RecordId? WithLook(IRecordStore store, string assetKey)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            return store.OfType<GroundLayerRecord>()
                .Where(layer => layer.AssetKey == assetKey)
                .OrderBy(layer => layer.Id)
                .Select(layer => (RecordId?)layer.Id)
                .FirstOrDefault();
        }
    }
}
