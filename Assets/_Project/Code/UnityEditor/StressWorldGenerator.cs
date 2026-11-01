using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Data.Terrain;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace Forge.UnityEditor
{
    /// <summary>
    /// Dev tool: generates the stress-test world, a 2 km × 2 km grid of terrain
    /// chunks scattered with objects, as a plugin on top of forge.core. It goes into
    /// its own StreamingAssets folder (git-ignored: it is large and can be regenerated) and is
    /// opened through the StressWorld profile. Deterministic.
    /// </summary>
    static class StressWorldGenerator
    {
        const string PluginId = "forge.stress";
        const string MasterId = "forge.core";
        const string Folder = "StressWorld";

        /// <summary>Chunks from the origin to each edge: the world spans [-HalfSize, HalfSize) on both axes.</summary>
        const int HalfSize = 16;

        /// <summary>forge.core already owns the terrain of this square around the origin.</summary>
        const int CoreRadius = 1;

        const int ObjectsPerChunk = 24;

        // Base objects from forge.core and the half height of their primitive at scale 1.
        static readonly (string Name, float HalfHeight)[] Bases =
        {
            ("boulder", 0.5f), ("crate", 0.5f), ("pillar", 1f), ("totem", 1f),
        };

        [MenuItem("Forge/Generate Stress World")]
        static void Generate()
        {
            var clock = Stopwatch.StartNew();
            var plugin = Build();
            var store = new PluginFileStore(Path.Combine(Application.streamingAssetsPath, Folder), new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()));
            store.Save(plugin);
            AssetDatabase.Refresh();

            var path = store.PathFor(PluginId);
            Debug.Log($"[Forge] Generated {path}: {plugin.Records.Count} records, {new FileInfo(path).Length / (1024.0 * 1024.0):0.0} MB in {clock.ElapsedMilliseconds} ms");
        }

        static Plugin Build()
        {
            var records = new List<Record>();
            for (var cz = -HalfSize; cz < HalfSize; cz++)
            for (var cx = -HalfSize; cx < HalfSize; cx++)
            {
                var coord = new ChunkCoord(cx, cz);
                if (Math.Abs(cx) <= CoreRadius && Math.Abs(cz) <= CoreRadius)
                    continue;

                records.Add(ProceduralTerrain.Chunk(PluginId, coord));
                ScatterObjects(coord, records);
            }

            return new Plugin(new PluginManifest(PluginId, "0.1.0", new[] { MasterId }), records);
        }

        static void ScatterObjects(ChunkCoord coord, List<Record> records)
        {
            // Seeded per chunk, so each chunk's content is stable however the world size changes.
            var random = new System.Random(unchecked(coord.X * 73856093 ^ coord.Z * 19349663));
            for (var i = 0; i < ObjectsPerChunk; i++)
            {
                var (name, halfHeight) = Bases[random.Next(Bases.Length)];
                var x = (coord.X + (float)random.NextDouble()) * WorldGrid.ChunkSize;
                var z = (coord.Z + (float)random.NextDouble()) * WorldGrid.ChunkSize;
                var scale = 0.6f + (float)random.NextDouble() * 1.9f;
                var yaw = (float)(random.NextDouble() * Math.PI * 2);

                records.Add(new PlacedObjectRecord(
                    new RecordId(PluginId, $"ref/c{coord.X}_{coord.Z}_{i:D2}"),
                    new RecordId(MasterId, name),
                    new Vector3(x, ProceduralTerrain.HeightAt(x, z) + halfHeight * scale, z),
                    Quaternion.CreateFromYawPitchRoll(yaw, 0f, 0f),
                    scale));
            }
        }
    }
}
