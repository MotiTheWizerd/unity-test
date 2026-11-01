using UnityEngine;

namespace Forge.EditorApp.Gizmos
{
    public static class GizmoHandleExtensions
    {
        /// <summary>The world axis of an axis handle; zero for center handles.</summary>
        public static Vector3 Direction(this GizmoHandle handle)
        {
            switch (handle)
            {
                case GizmoHandle.AxisX: return Vector3.right;
                case GizmoHandle.AxisY: return Vector3.up;
                case GizmoHandle.AxisZ: return Vector3.forward;
                default: return Vector3.zero;
            }
        }

        public static bool IsAxis(this GizmoHandle handle) =>
            handle == GizmoHandle.AxisX || handle == GizmoHandle.AxisY || handle == GizmoHandle.AxisZ;
    }
}
