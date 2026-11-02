using System;
using System.Collections.Generic;
using Forge.Editing.Placement;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Forge.EditorApp
{
    /// <summary>
    /// The one place that measures what placement needs from an asset's look (footprint and pivot
    /// height, at scale 1), so the ghost, drop to ground and sculpting all seat objects the same way.
    /// Each asset key is measured once, from a throwaway instance built by the world's asset catalog.
    /// </summary>
    public sealed class AssetShapes
    {
        readonly IAssetCatalog _catalog;
        readonly Dictionary<string, PlacementShape> _byKey = new Dictionary<string, PlacementShape>(StringComparer.Ordinal);

        public AssetShapes(IAssetCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public PlacementShape ShapeOf(string assetKey)
        {
            var key = assetKey ?? string.Empty;
            if (_byKey.TryGetValue(key, out var shape))
                return shape;

            var probe = _catalog.Instantiate(assetKey, null);
            probe.SetActive(false);
            shape = RendererBounds.TryGetLocal(probe, out var bounds)
                ? new PlacementShape(new Footprint(bounds.size.x, bounds.size.z), -bounds.min.y)
                : new PlacementShape(default, 0f);
            Object.Destroy(probe);

            _byKey.Add(key, shape);
            return shape;
        }
    }
}
