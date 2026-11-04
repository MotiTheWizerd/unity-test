using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Geometry;
using Forge.Editing.Placement;

namespace Forge.Editing.Transforms
{
    /// <summary>
    /// Slides objects over the ground: the pointer drags across the level plane through the point that was grabbed
    /// (so that point stays under the pointer), and each object is set down at its new spot the way placing seats it
    /// (<see cref="ObjectSeating"/>), keeping its heading and size: fences and rocks tilt to the slope there,
    /// everything else stands upright on it. Where there is no ground, an object moves level and keeps its height.
    /// </summary>
    public sealed class GroundSlide : ITransformDrag
    {
        readonly MoveDrag _move;
        readonly Vector3 _pivot;
        readonly ObjectSeating _seating;
        readonly Func<RecordId, BaseObjectRecord> _baseOf;

        GroundSlide(MoveDrag move, Vector3 pivot, ObjectSeating seating, Func<RecordId, BaseObjectRecord> baseOf)
        {
            _move = move;
            _pivot = pivot;
            _seating = seating;
            _baseOf = baseOf;
        }

        /// <summary>How far the objects have slid, level (Y is always 0).</summary>
        public Vector3 Delta => _move.Delta;

        public string Verb => "Move";

        public Vector3 HandlePosition => _pivot + Delta;

        public bool IsIdentity => _move.IsIdentity;

        public string Readout => _move.Readout;

        /// <summary>
        /// Starts a slide from <paramref name="grabPoint"/>, the point on an object the pointer pressed; null if the
        /// ray can't reach the level plane through it (looking straight along the horizon).
        /// </summary>
        /// <param name="pivot">Where the selection's gizmo stands; it follows the slide.</param>
        /// <param name="baseOf">The base object a placed object is an instance of, or null if it no longer exists.</param>
        public static GroundSlide TryBegin(Vector3 pivot, Vector3 grabPoint, Vector3 rayOrigin, Vector3 rayDirection,
            ObjectSeating seating, Func<RecordId, BaseObjectRecord> baseOf)
        {
            if (seating == null)
                throw new ArgumentNullException(nameof(seating));
            if (baseOf == null)
                throw new ArgumentNullException(nameof(baseOf));

            var move = MoveDrag.TryBegin(DragConstraint.Plane(grabPoint, Vector3.UnitY), rayOrigin, rayDirection);
            return move != null ? new GroundSlide(move, pivot, seating, baseOf) : null;
        }

        /// <summary>Follows the pointer; with <paramref name="snap"/>, the slide goes in <see cref="MoveDrag.SnapStep"/> steps.</summary>
        public void Update(Vector3 rayOrigin, Vector3 rayDirection, bool snap) => _move.Update(rayOrigin, rayDirection, snap);

        public PlacedObjectRecord Apply(PlacedObjectRecord original)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));
            if (IsIdentity)
                return original;

            var delta = Delta;
            var at = new Vector2(original.Position.X + delta.X, original.Position.Z + delta.Z);
            var baseObject = _baseOf(original.BaseId);
            var pose = baseObject != null
                ? _seating.SeatOnTerrain(baseObject, at, PlacementMath.HeadingOf(original.Rotation), original.Scale)
                : null;
            return pose.HasValue
                ? original.WithTransform(pose.Value.Position, pose.Value.Rotation, original.Scale)
                : original.WithPosition(original.Position + new Vector3(delta.X, 0f, delta.Z));
        }
    }
}
