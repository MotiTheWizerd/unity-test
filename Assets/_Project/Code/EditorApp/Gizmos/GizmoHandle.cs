namespace Forge.EditorApp.Gizmos
{
    /// <summary>
    /// A grabbable part of a gizmo. What dragging it does depends on the active
    /// transform mode: an axis handle moves along, rotates around, or scales along its axis.
    /// </summary>
    public enum GizmoHandle
    {
        AxisX,
        AxisY,
        AxisZ,

        /// <summary>Center of the move gizmo: free move on the horizontal plane.</summary>
        PlaneXZ,

        /// <summary>Center of the scale gizmo: uniform scale by distance from the pivot.</summary>
        Uniform,
    }
}
