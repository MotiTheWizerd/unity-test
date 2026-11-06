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
    /// How each material looks: a <see cref="MaterialRecord"/>'s asset key, found in the <see cref="MaterialCatalog"/>,
    /// gives its Unity material. A material the world or the catalog lacks gives nothing, with a warning once, so the
    /// slot keeps its base look and missing content never breaks an object.
    /// </summary>
    public sealed class MaterialLooks
    {
        readonly IReadOnlyList<MaterialCatalog> _catalogs;
        readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        IRecordStore _records;

        /// <param name="catalogs">The base game's catalog and every material pack's (<see cref="MaterialCatalog.LoadPacks"/>); the first that knows a key wins.</param>
        public MaterialLooks(IEnumerable<MaterialCatalog> catalogs)
        {
            if (catalogs == null)
                throw new ArgumentNullException(nameof(catalogs));
            var list = new List<MaterialCatalog>();
            foreach (var catalog in catalogs)
            {
                if (catalog != null)
                    list.Add(catalog);
            }

            if (list.Count == 0)
                throw new ArgumentException("At least one material catalog is required.", nameof(catalogs));
            _catalogs = list;
        }

        bool TryLook(string assetKey, out Material look)
        {
            foreach (var catalog in _catalogs)
            {
                if (catalog.TryGet(assetKey, out look))
                    return true;
            }

            look = null;
            return false;
        }

        /// <summary>The records materials are looked up in: the world on show.</summary>
        public void Use(IRecordStore records) => _records = records ?? throw new ArgumentNullException(nameof(records));

        /// <summary>The Unity material presenting <paramref name="material"/>, or null when it can't be found.</summary>
        public Material Of(RecordId material)
        {
            if (_records == null || !(_records.TryGet(material, out var record) && record is MaterialRecord found))
                return Missing(material.ToString(), $"there is no material '{material}'");
            return TryLook(found.AssetKey, out var look)
                ? look
                : Missing(found.AssetKey, $"the material catalog has nothing for '{found.AssetKey}' ({material})");
        }

        /// <summary>
        /// Dresses <paramref name="instance"/> (an asset built by the asset catalog) in <paramref name="materials"/>, slot
        /// by slot; its slots are captured first if they weren't (<see cref="MaterialSlots"/>).
        /// </summary>
        public void Dress(GameObject instance, IReadOnlyList<MaterialOverride> materials)
        {
            if (!instance.TryGetComponent<MaterialSlots>(out var slots))
                slots = MaterialSlots.Capture(instance);
            slots.Dress(materials ?? MaterialOverrides.None, Of);
        }

        /// <summary>A name for a look made of <paramref name="materials"/>, e.g. for caching its picture: "Roof=core:material/thatch;…".</summary>
        public static string KeyOf(IReadOnlyList<MaterialOverride> materials) =>
            materials == null || materials.Count == 0 ? null : string.Join(";", System.Linq.Enumerable.Select(materials, m => $"{m.Slot}={m.Material}"));

        /// <summary>
        /// A copy of <paramref name="material"/>'s look for its material ball (the caller destroys it, so the shared asset
        /// is never touched); null when it has no look.
        /// </summary>
        public Material CreatePreviewMaterial(RecordId material)
        {
            var look = Of(material);
            return look != null ? new Material(look) { name = $"Preview {look.name}" } : null;
        }

        Material Missing(string what, string problem)
        {
            if (_warned.Add(what))
                Debug.LogWarning($"[Forge] A slot keeps its base look where {problem}.");
            return null;
        }
    }
}
