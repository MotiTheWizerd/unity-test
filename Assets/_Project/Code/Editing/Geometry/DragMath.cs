using System;
using System.Numerics;

namespace Forge.Editing.Geometry
{
    /// <summary>
    /// Ray math behind gizmo handles: dragging along an axis, across a plane, and around an axis.
    /// Engine-free so it can be unit-tested and reused by any front end.
    /// </summary>
    public static class DragMath
    {
        const float ParallelEpsilon = 1e-6f;

        /// <summary>
        /// Finds the point on the infinite axis line closest to the ray, expressed as the
        /// distance <paramref name="t"/> along the (normalized) axis from <paramref name="axisOrigin"/>.
        /// Fails when the ray is (nearly) parallel to the axis.
        /// </summary>
        public static bool TryProjectOnAxis(Vector3 rayOrigin, Vector3 rayDirection, Vector3 axisOrigin, Vector3 axisDirection, out float t)
        {
            t = 0f;
            var a = Vector3.Normalize(axisDirection);
            var r = Vector3.Normalize(rayDirection);
            var w = axisOrigin - rayOrigin;

            var b = Vector3.Dot(a, r);
            var denominator = 1f - b * b;
            if (Math.Abs(denominator) < ParallelEpsilon)
                return false;

            // Closest points between two lines: axisOrigin + t*a and rayOrigin + s*r.
            var d = Vector3.Dot(a, w);
            var e = Vector3.Dot(r, w);
            t = (b * e - d) / denominator;
            return !float.IsNaN(t) && !float.IsInfinity(t);
        }

        /// <summary>
        /// The angle in radians that turns <paramref name="from"/> towards <paramref name="to"/>
        /// around <paramref name="axis"/>, in (-π, π], with the sign convention of
        /// <see cref="Quaternion.CreateFromAxisAngle"/>. Both vectors are projected onto the
        /// plane perpendicular to the axis first.
        /// </summary>
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            var n = Vector3.Normalize(axis);
            var a = from - n * Vector3.Dot(from, n);
            var b = to - n * Vector3.Dot(to, n);
            return (float)Math.Atan2(Vector3.Dot(n, Vector3.Cross(a, b)), Vector3.Dot(a, b));
        }

        /// <summary>Intersects the ray with a plane. Fails when the ray is parallel to it or points away.</summary>
        public static bool TryIntersectPlane(Vector3 rayOrigin, Vector3 rayDirection, Vector3 planePoint, Vector3 planeNormal, out Vector3 hit)
        {
            hit = default;
            var n = Vector3.Normalize(planeNormal);
            var denominator = Vector3.Dot(n, rayDirection);
            if (Math.Abs(denominator) < ParallelEpsilon)
                return false;

            var distance = Vector3.Dot(n, planePoint - rayOrigin) / denominator;
            if (distance < 0f)
                return false;

            hit = rayOrigin + rayDirection * distance;
            return true;
        }
    }
}
