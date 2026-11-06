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
        readonly MaterialCatalog _catalog;
        readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        IRecordStore _records;

        public MaterialLooks(MaterialCatalog catalog)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>The records materials are looked up in: the world on show.</summary>
        public void Use(IRecordStore records) => _records = records ?? throw new ArgumentNullException(nameof(records));

        /// <summary>The Unity material presenting <paramref name="material"/>, or null when it can't be found.</summary>
        public Material Of(RecordId material)
        {
            if (_records == null || !(_records.TryGet(material, out var record) && record is MaterialRecord found))
                return Missing(material.ToString(), $"there is no material '{material}'");
            return _catalog.TryGet(found.AssetKey, out var look)
                ? look
                : Missing(found.AssetKey, $"the material catalog has nothing for '{found.AssetKey}' ({material})");
        }

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
