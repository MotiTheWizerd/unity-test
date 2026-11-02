using System;
using System.Collections.Generic;
using System.IO;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Data.Terrain;
using UnityEditor;
using UnityEngine;
using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

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
                BaseObject("boulder", "Boulder", "primitive/sphere", "Nature/Rocks"),
                BaseObject("pillar", "Pillar", "primitive/cylinder", "Architecture"),
                BaseObject("totem", "Totem", "primitive/capsule", "Props/Decor"),
            };

            for (var cz = -ChunkRadius; cz <= ChunkRadius; cz++)
            for (var cx = -ChunkRadius; cx <= ChunkRadius; cx++)
                records.Add(ProceduralTerrain.Chunk(PluginId, new ChunkCoord(cx, cz)));

            // A ring of pillars around the origin, standing on the terrain.
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Math.PI * 2 / 6;
                var x = (float)Math.Cos(angle) * 20f;
                var z = (float)Math.Sin(angle) * 20f;
                records.Add(new PlacedObjectRecord(
                    new RecordId(PluginId, $"ref/pillar_{i + 1:D2}"),
                    new RecordId(PluginId, "pillar"),
                    new Vector3(x, ProceduralTerrain.HeightAt(x, z) + 1f, z),
                    Quaternion.Identity,
                    1f));
            }

            return new Plugin(new PluginManifest(PluginId, "0.1.0"), records);
        }

        static BaseObjectRecord BaseObject(string name, string displayName, string assetKey, string category) =>
            new BaseObjectRecord(new RecordId(PluginId, name), displayName, assetKey, category);

    }
}
