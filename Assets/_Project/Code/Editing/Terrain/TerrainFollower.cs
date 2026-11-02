using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Placement;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Keeps placed objects on sculpted ground. Each object over changed terrain moves
    /// vertically by exactly the change in ground height under its position, so its height
    /// above the ground stays the same: resting objects stay resting, floating ones keep
    /// floating, buried ones stay buried. Objects that align to the slope (fences, rocks) are
    /// also re-tilted to the new ground under their footprint, keeping the same clearance.
    /// Works on data only (no raycasts), so objects outside the loaded area follow too.
    /// </summary>
    public sealed class TerrainFollower
    {
        static readonly IReadOnlyList<PlacedObjectRecord> None = Array.Empty<PlacedObjectRecord>();

        // Grouped once per stroke, so each dab only looks at objects on the chunks it changed.
        readonly Dictionary<ChunkCoord, List<PlacedObjectRecord>> _byChunk = new Dictionary<ChunkCoord, List<PlacedObjectRecord>>();
        readonly Func<PlacedObjectRecord, PlacementShape?> _slopeShapeOf;

        /// <param name="world">The world as it was when the stroke began.</param>
        /// <param name="slopeShapeOf">
        /// The shape of an object that should be tilted to the slope, or null to only move it vertically
        /// (decided by the editor from the object's placement rules). Omit to never tilt.
        /// </param>
        public TerrainFollower(IRecordStore world, Func<PlacedObjectRecord, PlacementShape?> slopeShapeOf = null)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            _slopeShapeOf = slopeShapeOf ?? (_ => null);
            foreach (var placed in world.OfType<PlacedObjectRecord>())
            {
                var coord = WorldGrid.ChunkOf(placed.Position.X, placed.Position.Z);
                if (!_byChunk.TryGetValue(coord, out var list))
                    _byChunk.Add(coord, list = new List<PlacedObjectRecord>());
                list.Add(placed);
            }
        }

        /// <summary>
        /// Every object standing on a chunk the stroke touched, at the height (and tilt) it should
        /// have now. Objects whose ground didn't change come back unchanged, so a live preview can
        /// simply apply the whole list.
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
                    var shape = _slopeShapeOf(placed);
                    result.Add(shape.HasValue && TryRefit(placed, shape.Value, stroke, out var refitted)
                        ? refitted
                        : FollowVertically(placed, stroke));
                }
            }

            return result;
        }

        static PlacedObjectRecord FollowVertically(PlacedObjectRecord placed, SculptStroke stroke)
        {
            var change = stroke.HeightChangeAt(Flat(placed.Position)) ?? 0f;
            return change == 0f ? placed : placed.WithPosition(placed.Position + Vector3.UnitY * change);
        }

        // Seats the object on the ground before and after the stroke the same way placing does; the
        // difference between where it was and where the old ground would seat it is its clearance,
        // which it keeps. Ground that didn't change under the footprint leaves the object untouched.
        static bool TryRefit(PlacedObjectRecord placed, PlacementShape shape, SculptStroke stroke, out PlacedObjectRecord refitted)
        {
            refitted = placed;
            var center = Flat(placed.Position);
            var heading = PlacementMath.HeadingOf(placed.Rotation);
            if (!GroundFit.TryPose(center, shape, heading, placed.Scale, stroke.OriginalHeight, GroundFit.DefaultMaxTiltDegrees, out var before, out var oldRotation)
                || !GroundFit.TryPose(center, shape, heading, placed.Scale, stroke.SampleHeight, GroundFit.DefaultMaxTiltDegrees, out var after, out var rotation))
                return false;

            if (Vector3.DistanceSquared(before, after) < 1e-10f && Math.Abs(Quaternion.Dot(oldRotation, rotation)) > 1f - 1e-7f)
                return true;

            var clearance = placed.Position.Y - before.Y;
            refitted = placed.WithTransform(after + Vector3.UnitY * clearance, rotation, placed.Scale);
            return true;
        }

        static Vector2 Flat(Vector3 position) => new Vector2(position.X, position.Z);
    }
}
