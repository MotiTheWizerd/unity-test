using System;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.World;
using UnityEngine;

namespace Forge.Play.Sound
{
    /// <summary>
    /// What is underfoot at a point in the scene (<see cref="Surfaces"/>): looking down from just above it, the land's
    /// ground layer showing most there (<see cref="GroundPaint.DominantAt"/>), as its record says
    /// (<see cref="GroundLayerRecord.Surface"/>); null when that layer names none, or the foot is on a placed thing (a
    /// pier, a floor), which says nothing of itself yet, or on nothing. Its chunk's paint is read once and kept while
    /// the foot stays on it.
    /// </summary>
    public sealed class GroundSurfaces
    {
        // A look down starts this far above the point, and reaches this far below it.
        const float Above = 0.5f;
        const float Below = 1f;

        readonly WorldView _view;
        readonly WorldFrame _frame;
        readonly Func<IRecordStore> _world;
        readonly int _solid = Physics.DefaultRaycastLayers & ~ClutterLayer.Mask;
        TerrainChunkRecord _chunk;
        GroundPaint _paint;

        /// <param name="view">The land as shown, whose chunks say how they are painted.</param>
        /// <param name="frame">The world's frame in the scene.</param>
        /// <param name="world">The world as played, which the ground layers are read from; null while none is.</param>
        public GroundSurfaces(WorldView view, WorldFrame frame, Func<IRecordStore> world)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>What is underfoot at <paramref name="scene"/> (scene space), or null: nothing in particular.</summary>
        public string At(Vector3 scene)
        {
            if (!Physics.Raycast(scene + Vector3.up * Above, Vector3.down, out var hit, Above + Below, _solid, QueryTriggerInteraction.Ignore) ||
                !(hit.collider is TerrainCollider))
                return null;

            var world = _frame.ToWorld(hit.point);
            if (!_view.TryGetTerrain(WorldGrid.ChunkOf(world.x, world.z), out var chunk) || chunk.Paint == null)
                return null;
            if (!ReferenceEquals(chunk, _chunk))
            {
                _chunk = chunk;
                _paint = chunk.Paint.Load();
            }

            var (x, z) = WorldGrid.PaintTexelOf(world.x, world.z);
            var records = _world();
            return records != null && records.TryGet(_paint.DominantAt(x, z), out var record) && record is GroundLayerRecord layer
                ? layer.Surface
                : null;
        }
    }
}
