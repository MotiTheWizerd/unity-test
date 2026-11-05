using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing.Palette;

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
        /// The layers matching every term of <paramref name="query"/> in their display name or id, in their order
        /// (<see cref="SearchQuery"/>, as the object palette searches). A blank query returns them all.
        /// </summary>
        public static IReadOnlyList<GroundLayerRecord> Filter(IReadOnlyList<GroundLayerRecord> layers, string query)
        {
            if (layers == null)
                throw new ArgumentNullException(nameof(layers));

            var terms = SearchQuery.Terms(query);
            return terms.Length == 0
                ? layers
                : layers.Where(layer => SearchQuery.Matches(terms, layer.DisplayName, layer.Id.ToString())).ToList();
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
