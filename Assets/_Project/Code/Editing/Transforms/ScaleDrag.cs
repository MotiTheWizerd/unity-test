using System;
using System.Numerics;
using Forge.Data.Records;
using Forge.Editing.Geometry;

namespace Forge.Editing.Transforms
{
    /// <summary>
    /// Uniform scale (records hold one scale factor, like the Creation Kit). The
    /// factor is the pointer's distance from the pivot divided by the distance where
    /// the drag started: measured along the axis for axis handles, and radially on the
    /// plane for the center handle. With several objects, their positions spread from
    /// or gather towards the pivot.
    /// </summary>
    public sealed class ScaleDrag : ITransformDrag
    {
        /// <summary>Snap step of the factor.</summary>
        public const float SnapStep = 0.1f;

        /// <summary>Smallest factor one drag can apply, so a scale never reaches zero or flips.</summary>
        public const float MinFactor = 0.01f;

        const float MinStartDistance = 1e-4f;

        readonly DragConstraint _constraint;
        readonly float _start;

        ScaleDrag(DragConstraint constraint, float start)
        {
            _constraint = constraint;
            _start = start;
            Factor = 1f;
        }

        public float Factor { get; private set; }

        public string Verb => "Scale";

        public Vector3 HandlePosition => _constraint.Origin;

        public bool IsIdentity => Factor == 1f;

        public string Readout => $"×{Factor:0.00}";

        /// <summary>Starts a drag on <paramref name="constraint"/>; null if the ray misses it or starts on the pivot.</summary>
        public static ScaleDrag TryBegin(DragConstraint constraint, Vector3 rayOrigin, Vector3 rayDirection) =>
            TryMeasure(constraint, rayOrigin, rayDirection, out var start) && Math.Abs(start) > MinStartDistance
                ? new ScaleDrag(constraint, start)
                : null;

        public void Update(Vector3 rayOrigin, Vector3 rayDirection, bool snap)
        {
            if (!TryMeasure(_constraint, rayOrigin, rayDirection, out var current))
                return;

            var factor = Math.Max(MinFactor, current / _start);
            Factor = snap ? Math.Max(SnapStep, (float)Math.Round(factor / SnapStep) * SnapStep) : factor;
        }

        public PlacedObjectRecord Apply(PlacedObjectRecord original)
        {
            var pivot = _constraint.Origin;
            return original.WithTransform(
                pivot + (original.Position - pivot) * Factor,
                original.Rotation,
                original.Scale * Factor);
        }

        // Signed distance along a line (so dragging through the pivot shrinks), plain distance on a plane.
        static bool TryMeasure(DragConstraint constraint, Vector3 rayOrigin, Vector3 rayDirection, out float distance)
        {
            distance = 0f;
            if (!constraint.TryPoint(rayOrigin, rayDirection, out var point))
                return false;

            var offset = point - constraint.Origin;
            distance = constraint.IsPlane ? offset.Length() : Vector3.Dot(offset, constraint.Direction);
            return true;
        }
    }
}
