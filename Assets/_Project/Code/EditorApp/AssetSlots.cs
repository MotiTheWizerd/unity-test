using System;
using System.Collections.Generic;
using Forge.Data.Records;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Forge.EditorApp
{
    /// <summary>
    /// The one place that reads which material slots an asset has (<see cref="MaterialSlots"/>), so the selection options,
    /// the material balls and the agent all see the same slots, whether the object is loaded or not. Each asset key is
    /// read once, from a throwaway instance built by the world's asset catalog.
    /// </summary>
    public sealed class AssetSlots
    {
        readonly IAssetCatalog _catalog;
        readonly Dictionary<string, IReadOnlyList<string>> _byKey = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        public AssetSlots(IAssetCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>The slots of <paramref name="baseObject"/>'s asset, in the order its renderers first wear them.</summary>
        public IReadOnlyList<string> SlotsOf(BaseObjectRecord baseObject) => SlotsOf(baseObject?.AssetKey);

        public IReadOnlyList<string> SlotsOf(string assetKey)
        {
            var key = assetKey ?? string.Empty;
            if (_byKey.TryGetValue(key, out var slots))
                return slots;

            var probe = _catalog.Instantiate(assetKey, null);
            probe.SetActive(false);
            slots = MaterialSlots.Capture(probe).Slots;
            Object.Destroy(probe);

            _byKey.Add(key, slots);
            return slots;
        }
    }
}
