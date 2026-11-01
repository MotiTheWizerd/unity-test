using UnityEngine;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// The shapes of the move, rotate and scale gizmos, in gizmo space (about one
    /// unit across). The X/Y/Z colors and the hover tint come from <see cref="Gizmo"/>.
    /// </summary>
    public static class GizmoShapes
    {
        const int RingSegments = 48;
        const float RingThickness = 0.06f;
        const float StemThickness = 0.08f;

        static readonly GizmoHandle[] Axes = { GizmoHandle.AxisX, GizmoHandle.AxisY, GizmoHandle.AxisZ };

        /// <summary>Arrows along X/Y/Z, plus a center ball for moving on the ground plane.</summary>
        public static Gizmo Translate(int layer, Material material)
        {
            var gizmo = new Gizmo("TranslateGizmo", layer, material);
            foreach (var axis in Axes)
            {
                AddStem(gizmo, axis, 1f);
                AddTip(gizmo, axis, PrimitiveType.Cube, 1.05f, 0.2f);
            }

            gizmo.AddPart(GizmoHandle.PlaneXZ, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity, Vector3.one * 0.16f);
            return gizmo;
        }

        /// <summary>One ring around each axis, built from short cylinder segments.</summary>
        public static Gizmo Rotate(int layer, Material material)
        {
            var gizmo = new Gizmo("RotateGizmo", layer, material);
            var segmentLength = 2f * Mathf.PI / RingSegments;
            foreach (var axis in Axes)
            {
                var direction = axis.Direction();
                // Two unit vectors spanning the ring's plane.
                var u = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var v = Vector3.Cross(direction, u);
                for (var i = 0; i < RingSegments; i++)
                {
                    var angle = (i + 0.5f) * segmentLength;
                    var position = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    var tangent = -u * Mathf.Sin(angle) + v * Mathf.Cos(angle);
                    // A cylinder primitive is 2 units tall; overlap a little so the ring has no gaps.
                    gizmo.AddPart(axis, PrimitiveType.Cylinder, position, Quaternion.FromToRotation(Vector3.up, tangent),
                        new Vector3(RingThickness, segmentLength * 0.55f, RingThickness));
                }
            }

            return gizmo;
        }

        /// <summary>Short stems with cube tips (uniform scale along any axis), plus a center cube.</summary>
        public static Gizmo Scale(int layer, Material material)
        {
            var gizmo = new Gizmo("ScaleGizmo", layer, material);
            foreach (var axis in Axes)
            {
                AddStem(gizmo, axis, 0.8f);
                AddTip(gizmo, axis, PrimitiveType.Cube, 0.85f, 0.18f);
            }

            gizmo.AddPart(GizmoHandle.Uniform, PrimitiveType.Cube, Vector3.zero, Quaternion.identity, Vector3.one * 0.22f);
            return gizmo;
        }

        static void AddStem(Gizmo gizmo, GizmoHandle axis, float length)
        {
            var direction = axis.Direction();
            gizmo.AddPart(axis, PrimitiveType.Cylinder, direction * (length * 0.5f), Quaternion.FromToRotation(Vector3.up, direction),
                new Vector3(StemThickness, length * 0.5f, StemThickness));
        }

        static void AddTip(Gizmo gizmo, GizmoHandle axis, PrimitiveType type, float distance, float size)
        {
            var direction = axis.Direction();
            gizmo.AddPart(axis, type, direction * distance, Quaternion.FromToRotation(Vector3.up, direction), Vector3.one * size);
        }
    }
}
