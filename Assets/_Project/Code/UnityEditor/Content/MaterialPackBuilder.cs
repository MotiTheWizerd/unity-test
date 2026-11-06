using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.World.Assets;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Dev tool: turns each material pack under Content/MaterialPacks into what the game reads, the same way the base
    /// game's own materials are built. A pack is a folder holding a pack.json (<see cref="PackManifest"/>: the plugin it
    /// becomes and, per material, its folder, name and kind of surface) and a Materials folder of texture folders in
    /// ambientCG's naming. Building it makes each folder's material, lists them in the pack's own catalog under
    /// Resources/MaterialPacks (where the editor finds every pack's catalog at start), and writes the pack's plugin of
    /// material records to StreamingAssets/Plugins. A pack is self-contained, so one kept out of git (a local test pack)
    /// leaves nothing committed pointing at files that aren't there.
    /// </summary>
    static class MaterialPackBuilder
    {
        public const string PacksFolder = "Assets/_Project/Content/MaterialPacks";

        const string ManifestFile = "pack.json";
        const string MaterialsSubfolder = "Materials";

        [MenuItem("Forge/Materials/Build Material Packs")]
        static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(PacksFolder))
            {
                Debug.Log($"[Forge] No material packs: {PacksFolder} doesn't exist.");
                return;
            }

            foreach (var pack in AssetDatabase.GetSubFolders(PacksFolder).OrderBy(f => f, StringComparer.Ordinal))
            {
                var manifestPath = Path.Combine(pack, ManifestFile);
                if (File.Exists(manifestPath))
                    Build(pack, JsonUtility.FromJson<PackManifest>(File.ReadAllText(manifestPath)));
            }
        }

        static void Build(string pack, PackManifest manifest)
        {
            var name = Path.GetFileName(pack);
            if (string.IsNullOrWhiteSpace(manifest?.pluginId) || manifest.materials == null)
            {
                Debug.LogError($"[Forge] {pack}/{ManifestFile} needs a pluginId and a materials list; skipped.");
                return;
            }

            var resources = EnsureFolder(EnsureFolder(pack, "Resources"), MaterialCatalog.PacksResourcesFolder);
            var catalog = MaterialBuilder.LoadOrCreateCatalog($"{resources}/{name}.asset");
            var built = MaterialBuilder.BuildFolders($"{pack}/{MaterialsSubfolder}", catalog, folder => KeyFor(name, folder));

            // Only materials whose textures were built become records, so the plugin never names a look that isn't there.
            var records = new List<Record>();
            foreach (var entry in manifest.materials)
            {
                if (!catalog.TryGet(KeyFor(name, entry.folder), out _))
                {
                    Debug.LogWarning($"[Forge] Pack {name}: no built material for '{entry.folder}'; left out of the plugin.");
                    continue;
                }

                records.Add(new MaterialRecord(MaterialRecord.IdFor(manifest.pluginId, entry.folder.ToLowerInvariant()),
                    string.IsNullOrWhiteSpace(entry.name) ? entry.folder : entry.name, KeyFor(name, entry.folder), entry.category));
            }

            var store = new PluginFileStore(Path.Combine(Application.streamingAssetsPath, "Plugins"),
                new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()));
            store.Save(new Plugin(new PluginManifest(manifest.pluginId, "0.1.0"), records));
            AssetDatabase.Refresh();
            Debug.Log($"[Forge] Pack {name}: built {built} materials, wrote {records.Count} records to {store.PathFor(manifest.pluginId)}.");
        }

        /// <summary>A pack material's asset key: "material/{pack}/{folder}", lower case, so packs never clash with each other or the base game.</summary>
        static string KeyFor(string pack, string folder) => $"material/{pack.ToLowerInvariant()}/{folder.ToLowerInvariant()}";

        static string EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
            return path;
        }

        /// <summary>A pack's pack.json.</summary>
        [Serializable]
        sealed class PackManifest
        {
            /// <summary>The plugin the pack's records go in, e.g. "forge.testpack".</summary>
            public string pluginId;

            public PackMaterial[] materials;
        }

        /// <summary>One material of a pack.</summary>
        [Serializable]
        sealed class PackMaterial
        {
            /// <summary>Its texture folder under Materials, in ambientCG's naming ({folder}_Color.jpg …).</summary>
            public string folder;

            /// <summary>Its name for people; the folder's when left out.</summary>
            public string name;

            /// <summary>Its kind of surface, the library tab it shows under ("Wood", "Fabric").</summary>
            public string category;

            /// <summary>Where the textures came from (an ambientCG asset id), for the fetch script and the credits.</summary>
            public string source;
        }
    }
}
