using System.Collections.Generic;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Data.Terrain;
using Forge.UnityEditor.Content;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor
{
    /// <summary>
    /// Dev tool: generates the base-game plugin (forge.core) that ships in
    /// StreamingAssets, i.e. our equivalent of Skyrim.esm. Deterministic, so
    /// regenerating produces an identical file.
    /// </summary>
    static class CorePluginGenerator
    {
        const string PluginId = "forge.core";
        const int ChunkRadius = 1;

        /// <summary>The main worldspace, where the editor opens (see the world profiles).</summary>
        public static readonly RecordId MainSpace = SpaceRecord.IdFor(PluginId, "main");

        [MenuItem("Forge/Generate Core Plugin")]
        static void Generate()
        {
            var directory = Path.Combine(Application.streamingAssetsPath, "Plugins");
            var store = new PluginFileStore(directory, new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()));
            store.Save(Build());
            AssetDatabase.Refresh();
            Debug.Log($"[Forge] Generated {store.PathFor(PluginId)}");
        }

        static Plugin Build()
        {
            var records = new List<Record>
            {
                BaseObject("crate", "Crate", "primitive/cube", "Props/Containers"),
                BaseObject("boulder", "Boulder", "primitive/sphere", "Nature/Rocks", PlaceholderContent.Natural(0.6f, 1.6f, alignToSlope: true)),
                BaseObject("pillar", "Pillar", "primitive/cylinder", "Architecture"),
                BaseObject("totem", "Totem", "primitive/capsule", "Props/Decor"),
            };
            records.AddRange(PlaceholderContent.All.Select(c => BaseObject(c.Name, c.DisplayName, c.AssetKey, c.Category, c.Placement)));

            records.AddRange(CoreClimates.All(PluginId));
            records.AddRange(CoreGroundLayers.All(PluginId));
            records.AddRange(CoreMaterials.All(PluginId));
            records.AddRange(CoreAttributeSet.All());

            // Bare land: the base world ships the objects to build with, and nothing placed.
            records.Add(new SpaceRecord(MainSpace, "Main World", SpaceKind.Worldspace, ProceduralTerrain.BaseHeight, climate: CoreClimates.Temperate(PluginId)));
            for (var cz = -ChunkRadius; cz <= ChunkRadius; cz++)
            for (var cx = -ChunkRadius; cx <= ChunkRadius; cx++)
                records.Add(ProceduralTerrain.Chunk(PluginId, MainSpace, new ChunkCoord(cx, cz)));

            return new Plugin(new PluginManifest(PluginId, "0.1.0"), records);
        }

        static BaseObjectRecord BaseObject(string name, string displayName, string assetKey, string category, PlacementRules placement = null) =>
            new BaseObjectRecord(new RecordId(PluginId, name), displayName, assetKey, category, placement);

    }
}
