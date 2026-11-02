using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Keeps placed objects on sculpted ground. Each object over changed terrain moves
    /// vertically by exactly the change in ground height under its position, so its height
    /// above the ground stays the same: resting objects stay resting, floating ones keep
    /// floating, buried ones stay buried. Works on data only (no raycasts), so objects
    /// outside the loaded area follow too.
    /// </summary>
    public sealed class TerrainFollower
    {
        static readonly IReadOnlyList<PlacedObjectRecord> None = Array.Empty<PlacedObjectRecord>();

        // Grouped once per stroke, so each dab only looks at objects on the chunks it changed.
        readonly Dictionary<ChunkCoord, List<PlacedObjectRecord>> _byChunk = new Dictionary<ChunkCoord, List<PlacedObjectRecord>>();

        /// <param name="world">The world as it was when the stroke began.</param>
        public TerrainFollower(IRecordStore world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            foreach (var placed in world.OfType<PlacedObjectRecord>())
            {
                var coord = WorldGrid.ChunkOf(placed.Position.X, placed.Position.Z);
                if (!_byChunk.TryGetValue(coord, out var list))
                    _byChunk.Add(coord, list = new List<PlacedObjectRecord>());
                list.Add(placed);
            }
        }

        /// <summary>
        /// Every object standing on a chunk the stroke touched, at the height it should
        /// have now. Objects whose ground didn't change come back unchanged, so a live
        /// preview can simply apply the whole list.
        /// </summary>
        public IReadOnlyList<PlacedObjectRecord> Follow(SculptStroke stroke)
        {
            if (stroke == null)
                throw new ArgumentNullException(nameof(stroke));
            if (stroke.TouchedChunks.Count == 0)
                return None;

            var result = new List<PlacedObjectRecord>();
            foreach (var coord in stroke.TouchedChunks)
            {
                if (!_byChunk.TryGetValue(coord, out var objects))
                    continue;

                foreach (var placed in objects)
                {
                    var change = stroke.HeightChangeAt(new Vector2(placed.Position.X, placed.Position.Z)) ?? 0f;
                    result.Add(change == 0f ? placed : placed.WithPosition(placed.Position + Vector3.UnitY * change));
                }
            }

            return result;
        }
    }
}
