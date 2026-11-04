using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing.Terrain;
using Forge.Editing.Water;
using Forge.World;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// The water of the space on show: its <see cref="WaterMap"/> (the one data answer, which the Water tool reads
    /// too) and the scene surfaces for it, the sea and each lake. Brought up to date when the space or its water
    /// can have changed: a space switch, or an edit to the space, a lake or the terrain (a lake's shore follows
    /// sculpting). Lakes that spill show nothing.
    /// </summary>
    public sealed class WaterView : IDisposable
    {
        readonly SeaSurface _sea;
        readonly LakeSurfaces _lakes;
        ResolvedWorld _world;
        RecordId _space;
        RecordId? _previewReplaces;

        /// <param name="material">The water's look; null shows no water (the map is still kept).</param>
        /// <param name="seaSize">Edge length of the sea surface; twice the camera's far clip reaches the horizon.</param>
        public WaterView(Transform parent, Material material, float seaSize)
        {
            if (material != null)
            {
                _sea = SeaSurface.Create(parent, material, seaSize);
                _lakes = new LakeSurfaces(parent, material);
            }
        }

        /// <summary>The water of the space on show, as data; null before the first <see cref="Sync"/>.</summary>
        public WaterMap Map { get; private set; }

        /// <summary>Shows <paramref name="space"/>'s water in <paramref name="world"/>, rebuilding only when it can have changed.</summary>
        public void Sync(ResolvedWorld world, RecordId space)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            var previous = _world;
            _world = world;
            if (Map != null && space == _space && world.TryGetChangesSince(previous, out var changed)
                && !changed.Any(id => AffectsWater(previous.Records, id) || AffectsWater(world.Records, id)))
                return;

            _space = space;
            Map = WaterMap.Of(world.Records, new TerrainGrid(world.Records, space));
            if (_sea == null)
                return;

            _sea.Show(Map.SeaLevel);
            _lakes.HideAll();
            foreach (var lake in Map.Lakes.Where(lake => !lake.Extent.IsEmpty))
                _lakes.Show(lake.Record.Id, lake.Record.Level, Map.Terrain.Spacing, lake.Extent.CellRuns());
        }

        /// <summary>Keeps the sea under the camera, so it reaches the horizon wherever the camera flies.</summary>
        public void Follow(Vector3 focus)
        {
            if (_sea != null)
                _sea.Follow(focus);
        }

        /// <summary>
        /// Shows <paramref name="extent"/> at <paramref name="level"/> in place of the lake <paramref name="replaces"/>
        /// (null for a new one) while a tool sets its level. Nothing is saved; <see cref="EndPreview"/> takes it away.
        /// </summary>
        public void Preview(LakeExtent extent, float level, RecordId? replaces)
        {
            if (_lakes == null || Map == null)
                return;

            if (_previewReplaces.HasValue && _previewReplaces != replaces)
                _lakes.SetVisible(_previewReplaces.Value, true);
            _previewReplaces = replaces;
            if (replaces.HasValue)
                _lakes.SetVisible(replaces.Value, false);
            if (extent.IsEmpty)
                _lakes.HidePreview();
            else
                _lakes.ShowPreview(level, Map.Terrain.Spacing, extent.CellRuns());
        }

        public void EndPreview()
        {
            if (_lakes == null)
                return;
            _lakes.HidePreview();
            if (_previewReplaces.HasValue)
                _lakes.SetVisible(_previewReplaces.Value, true);
            _previewReplaces = null;
        }

        public void Dispose()
        {
            _lakes?.Dispose();
            if (_sea != null)
                UnityEngine.Object.Destroy(_sea.gameObject);
        }

        static bool AffectsWater(IRecordStore store, RecordId id) =>
            store.TryGet(id, out var record) && (record is SpaceRecord || record is LakeRecord || record is TerrainChunkRecord);
    }
}
