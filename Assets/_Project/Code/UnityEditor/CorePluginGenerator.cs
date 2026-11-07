using System;
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
using Application = UnityEngine.Application;
using Compass = Forge.Core.Geometry.Compass;
using Debug = UnityEngine.Debug;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

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

        // Meters between the items laid out at the start, and how far ahead of the start their row lies: a step away,
        // in sight when looking a little down.
        const float ItemSpacing = 0.8f;
        const float ItemsAhead = 2.5f;

        // The bare earth around the start, so grass doesn't hide the items: dirt within the inner radius (m) of the
        // items' row, fading into the grass by the outer.
        const float ClearingInner = 3.5f;
        const float ClearingOuter = 5f;

        /// <summary>The main worldspace, where the editor opens (see the world profiles).</summary>
        public static readonly RecordId MainSpace = SpaceRecord.IdFor(PluginId, "main");

        // One chunk of land, 64 m a side: small enough that nothing in it is hard to find.
        static readonly ChunkCoord MainChunk = new ChunkCoord(0, 0);

        // The middle of the land, a little south of it so the items ahead are in the middle; facing north.
        static readonly PlayerStart MainStart = new PlayerStart(
            new Vector3((MainChunk.X + 0.5f) * WorldGrid.ChunkSize, ProceduralTerrain.BaseHeight, (MainChunk.Z + 0.5f) * WorldGrid.ChunkSize - ItemsAhead),
            0f);

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
            records.AddRange(PlaceholderContent.All.Select(c => BaseObject(c.Name, c.DisplayName, c.AssetKey, c.Category, c.Placement, c.Item)));
            records.AddRange(CoreModels.All(PluginId));

            records.AddRange(CoreClimates.All(PluginId));
            records.AddRange(CoreGroundLayers.All(PluginId));
            records.AddRange(CoreMaterials.All(PluginId));
            records.AddRange(CoreAttributeSet.All());
            records.AddRange(CoreEquipSlotSet.All());
            records.AddRange(CoreActorSet.All(PluginId));
            records.AddRange(CoreCombatStyleSet.All());

            // A small piece of bare land with a start in its middle and every item of the base game laid out ahead of it,
            // so a game begins beside them; everything else is the objects to build with.
            records.Add(new SpaceRecord(MainSpace, "Main World", SpaceKind.Worldspace, ProceduralTerrain.BaseHeight, climate: CoreClimates.Temperate(PluginId),
                playerStart: MainStart));
            records.Add(ProceduralTerrain.Chunk(PluginId, MainSpace, MainChunk).WithPaint(StartClearing()));
            records.AddRange(ItemsAtTheStart(records.OfType<BaseObjectRecord>().Where(r => r.Item != null).ToList()));

            return new Plugin(new PluginManifest(PluginId, "0.1.0"), records);
        }

        // Where the items lie: ItemsAhead in front of the start.
        static Vector3 ItemsCentre()
        {
            var ahead = Compass.Forward(MainStart.Heading) * ItemsAhead;
            return MainStart.Position + new Vector3(ahead.X, 0f, ahead.Y);
        }

        // The main chunk's paint: grass, and a round clearing of dirt around the items.
        static GroundPaint StartClearing()
        {
            var layers = new[] { GroundLayerRecord.IdFor(PluginId, "grass"), GroundLayerRecord.IdFor(PluginId, "dirt") };
            var weights = new float[layers.Length * GroundPaint.TexelCount];
            var centre = ItemsCentre();
            var texel = WorldGrid.ChunkSize / GroundPaint.Resolution;
            for (var z = 0; z < GroundPaint.Resolution; z++)
            for (var x = 0; x < GroundPaint.Resolution; x++)
            {
                // The texel's middle, in world meters.
                var dx = MainChunk.X * WorldGrid.ChunkSize + (x + 0.5f) * texel - centre.X;
                var dz = MainChunk.Z * WorldGrid.ChunkSize + (z + 0.5f) * texel - centre.Z;
                var fade = (ClearingOuter - (float)Math.Sqrt(dx * dx + dz * dz)) / (ClearingOuter - ClearingInner);
                var dirt = Math.Max(0f, Math.Min(1f, fade));
                weights[z * GroundPaint.Resolution + x] = 1f - dirt;
                weights[GroundPaint.TexelCount + z * GroundPaint.Resolution + x] = dirt;
            }

            return GroundPaint.FromWeights(layers, weights);
        }

        // One of each item in a row across the start's view, ItemsAhead in front of it, centred on its line of sight. Every
        // asset's pivot is its ground contact point, so each stands at the land's height.
        static IEnumerable<PlacedObjectRecord> ItemsAtTheStart(IReadOnlyList<BaseObjectRecord> items)
        {
            var right = Compass.Right(MainStart.Heading);
            var facing = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MainStart.Heading * Compass.DegreesToRadians);
            var centre = ItemsCentre();
            for (var i = 0; i < items.Count; i++)
            {
                var across = right * ((i - (items.Count - 1) / 2f) * ItemSpacing);
                var position = centre + new Vector3(across.X, 0f, across.Y);
                yield return new PlacedObjectRecord(new RecordId(PluginId, "ref/start_" + items[i].Id.Name.Replace('/', '_')), items[i].Id, MainSpace,
                    position, facing, 1f);
            }
        }

        static BaseObjectRecord BaseObject(string name, string displayName, string assetKey, string category, PlacementRules placement = null,
            ItemPart item = null) =>
            new BaseObjectRecord(new RecordId(PluginId, name), displayName, assetKey, category, placement, item: item);

    }
}
