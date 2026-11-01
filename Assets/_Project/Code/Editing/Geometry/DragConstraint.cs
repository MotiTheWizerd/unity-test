using System.Numerics;

namespace Forge.Editing.Geometry
{
    /// <summary>
    /// What a gizmo handle constrains the pointer to: a line (axis handles) or a
    /// plane (center handles, rotation rings). Turns a pointer ray into a point on it.
    /// </summary>
    public readonly struct DragConstraint
    {
        DragConstraint(Vector3 origin, Vector3 direction, bool isPlane)
        {
            Origin = origin;
            Direction = Vector3.Normalize(direction);
            IsPlane = isPlane;
        }

        /// <summary>A point on the line or plane (the gizmo pivot).</summary>
        public Vector3 Origin { get; }

        /// <summary>The line direction, or the plane normal; normalized.</summary>
        public Vector3 Direction { get; }

        public bool IsPlane { get; }

        public static DragConstraint Line(Vector3 origin, Vector3 direction) => new DragConstraint(origin, direction, false);

        public static DragConstraint Plane(Vector3 point, Vector3 normal) => new DragConstraint(point, normal, true);

        /// <summary>
        /// The point on the constraint the ray points at: the closest point on a line,
        /// or the intersection with a plane. Fails for parallel rays or planes behind the ray.
        /// </summary>
        public bool TryPoint(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 point)
        {
            if (IsPlane)
                return DragMath.TryIntersectPlane(rayOrigin, rayDirection, Origin, Direction, out point);

            point = default;
            if (!DragMath.TryProjectOnAxis(rayOrigin, rayDirection, Origin, Direction, out var t))
                return false;
            point = Origin + Direction * t;
            return true;
        }
    }
}
