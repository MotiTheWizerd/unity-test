using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// Sets new objects down on the ground. Objects that tilt to the slope are seated on the terrain under
    /// their footprint (<see cref="GroundFit"/>); everything else stands upright with its lowest point on the
    /// ground. The one seating rule of the place tool and the world-building commands, so the agent's
    /// objects sit exactly like hand-placed ones. Engine-free: the ground and the shapes come from the caller.
    /// </summary>
    public sealed class ObjectSeating
    {
        readonly Func<Vector2, float?> _terrainHeightAt;
        readonly Func<RecordId, PlacementShape?> _slopeShapeOf;
        readonly Func<string, PlacementShape> _shapeOf;

        /// <param name="terrainHeightAt">Terrain height at a ground position (x, z), or null where there is no terrain.</param>
        /// <param name="slopeShapeOf">The shape to tilt instances of a base object with, or null if they stay upright.</param>
        /// <param name="shapeOf">An asset's measured shape (footprint and pivot height) at scale 1, by asset key.</param>
        public ObjectSeating(Func<Vector2, float?> terrainHeightAt, Func<RecordId, PlacementShape?> slopeShapeOf, Func<string, PlacementShape> shapeOf)
        {
            _terrainHeightAt = terrainHeightAt ?? throw new ArgumentNullException(nameof(terrainHeightAt));
            _slopeShapeOf = slopeShapeOf ?? throw new ArgumentNullException(nameof(slopeShapeOf));
            _shapeOf = shapeOf ?? throw new ArgumentNullException(nameof(shapeOf));
        }

        /// <summary>The footprint and pivot height of <paramref name="baseObject"/>'s look, at scale 1 and no rotation.</summary>
        public PlacementShape ShapeOf(BaseObjectRecord baseObject)
        {
            if (baseObject == null)
                throw new ArgumentNullException(nameof(baseObject));
            return _shapeOf(baseObject.AssetKey);
        }

        /// <summary>
        /// The pose of <paramref name="baseObject"/> resting on <paramref name="ground"/>, heading
        /// <paramref name="yawDegrees"/>, at <paramref name="scale"/>. Only terrain tilts objects: one resting
        /// on another object (<paramref name="onTerrain"/> false) stays upright.
        /// </summary>
        public ObjectPose Seat(BaseObjectRecord baseObject, Vector3 ground, bool onTerrain, float yawDegrees, float scale)
        {
            if (baseObject == null)
                throw new ArgumentNullException(nameof(baseObject));

            var slopeShape = onTerrain ? _slopeShapeOf(baseObject.Id) : null;
            if (slopeShape.HasValue && GroundFit.TryPose(new Vector2(ground.X, ground.Z), slopeShape.Value, yawDegrees, scale,
                    _terrainHeightAt, GroundFit.DefaultMaxTiltDegrees, out var pivot, out var rotation))
                return new ObjectPose(pivot, rotation);

            // The pivot-to-bottom distance grows with the scale.
            var lift = ShapeOf(baseObject).PivotAboveBottom * scale;
            return new ObjectPose(ground + Vector3.UnitY * lift, GroundFit.Orient(Vector3.UnitY, yawDegrees));
        }

        /// <summary>The pose of <paramref name="baseObject"/> standing on the terrain at <paramref name="at"/> (x, z), or null where there is no terrain.</summary>
        public ObjectPose? SeatOnTerrain(BaseObjectRecord baseObject, Vector2 at, float yawDegrees, float scale)
        {
            var height = _terrainHeightAt(at);
            return height.HasValue
                ? Seat(baseObject, new Vector3(at.X, height.Value, at.Y), true, yawDegrees, scale)
                : (ObjectPose?)null;
        }
    }
}
