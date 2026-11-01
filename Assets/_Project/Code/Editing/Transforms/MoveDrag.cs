using System;
using System.Numerics;
using Forge.Data.Records;
using Forge.Editing.Geometry;

namespace Forge.Editing.Transforms
{
    /// <summary>Moves along an axis or across a plane by the pointer's travel on that constraint.</summary>
    public sealed class MoveDrag : ITransformDrag
    {
        /// <summary>Snap step in meters.</summary>
        public const float SnapStep = 0.5f;

        readonly DragConstraint _constraint;
        readonly Vector3 _start;

        MoveDrag(DragConstraint constraint, Vector3 start)
        {
            _constraint = constraint;
            _start = start;
        }

        public Vector3 Delta { get; private set; }

        public string Verb => "Move";

        public Vector3 HandlePosition => _constraint.Origin + Delta;

        public bool IsIdentity => Delta == Vector3.Zero;

        public string Readout => $"{Delta.Length():0.00} m";

        /// <summary>Starts a drag on <paramref name="constraint"/>; null if the ray doesn't reach it.</summary>
        public static MoveDrag TryBegin(DragConstraint constraint, Vector3 rayOrigin, Vector3 rayDirection) =>
            constraint.TryPoint(rayOrigin, rayDirection, out var start) ? new MoveDrag(constraint, start) : null;

        public void Update(Vector3 rayOrigin, Vector3 rayDirection, bool snap)
        {
            if (!_constraint.TryPoint(rayOrigin, rayDirection, out var current))
                return;

            var delta = current - _start;
            var n = _constraint.Direction;
            if (_constraint.IsPlane)
            {
                // Stay in the plane exactly, even with float error in the hit point.
                delta -= n * Vector3.Dot(delta, n);
                Delta = snap ? new Vector3(Snap(delta.X), Snap(delta.Y), Snap(delta.Z)) : delta;
            }
            else
            {
                var along = Vector3.Dot(delta, n);
                Delta = n * (snap ? Snap(along) : along);
            }
        }

        public PlacedObjectRecord Apply(PlacedObjectRecord original) =>
            original.WithPosition(original.Position + Delta);

        static float Snap(float value) => (float)Math.Round(value / SnapStep) * SnapStep;
    }
}
