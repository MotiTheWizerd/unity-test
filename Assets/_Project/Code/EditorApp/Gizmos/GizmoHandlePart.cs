using UnityEngine;

namespace Forge.EditorApp.Gizmos
{
    /// <summary>Marks a collider as part of a gizmo handle so picks can be mapped back to it.</summary>
    [DisallowMultipleComponent]
    public sealed class GizmoHandlePart : MonoBehaviour
    {
        public GizmoHandle Handle { get; internal set; }
    }
}
