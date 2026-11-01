using System;
using System.Numerics;
using Forge.Data.Records;
using Forge.Editing.Geometry;

namespace Forge.Editing.Transforms
{
    /// <summary>
    /// Rotates around a world axis through the pivot, following the pointer around
    /// the ring. The angle accumulates, so it keeps growing past a full turn. With
    /// several objects, their positions turn around the pivot too.
    /// </summary>
    public sealed class RotateDrag : ITransformDrag
    {
        /// <summary>Snap step in degrees.</summary>
        public const float SnapDegrees = 15f;

        const float MinRadius = 1e-4f;

        readonly DragConstraint _plane;
        Vector3 _last;
        float _angle;

        RotateDrag(DragConstraint plane, Vector3 start)
        {
            _plane = plane;
            _last = start;
        }

        /// <summary>The applied angle in degrees (snapped if snapping).</summary>
        public float Degrees { get; private set; }

        public Quaternion Rotation => Quaternion.CreateFromAxisAngle(_plane.Direction, Degrees * (float)(Math.PI / 180));

        public string Verb => "Rotate";

        public Vector3 HandlePosition => _plane.Origin;

        public bool IsIdentity => Degrees == 0f;

        public string Readout => $"{Degrees:0.#}°";

        /// <summary>Starts rotating around <paramref name="axis"/>; null if the ray misses the ring's plane or hits the pivot.</summary>
        public static RotateDrag TryBegin(Vector3 pivot, Vector3 axis, Vector3 rayOrigin, Vector3 rayDirection)
        {
            var plane = DragConstraint.Plane(pivot, axis);
            return TryRadius(plane, rayOrigin, rayDirection, out var start) ? new RotateDrag(plane, start) : null;
        }

        public void Update(Vector3 rayOrigin, Vector3 rayDirection, bool snap)
        {
            if (!TryRadius(_plane, rayOrigin, rayDirection, out var current))
                return;

            // Sum small steps rather than measuring from the start, so angles beyond ±180° don't wrap.
            _angle += DragMath.SignedAngle(_last, current, _plane.Direction);
            _last = current;

            var degrees = _angle * (float)(180 / Math.PI);
            Degrees = snap ? (float)Math.Round(degrees / SnapDegrees) * SnapDegrees : degrees;
        }

        public PlacedObjectRecord Apply(PlacedObjectRecord original)
        {
            var rotation = Rotation;
            var pivot = _plane.Origin;
            return original.WithTransform(
                pivot + Vector3.Transform(original.Position - pivot, rotation),
                Quaternion.Normalize(Quaternion.Concatenate(original.Rotation, rotation)),
                original.Scale);
        }

        static bool TryRadius(DragConstraint plane, Vector3 rayOrigin, Vector3 rayDirection, out Vector3 radius)
        {
            radius = default;
            if (!plane.TryPoint(rayOrigin, rayDirection, out var hit))
                return false;
            radius = hit - plane.Origin;
            return radius.Length() > MinRadius;
        }
    }
}
