namespace Forge.EditorApp.Tools
{
    /// <summary>The editor's tools, one active at a time. <see cref="ToolCatalog"/> describes each one.</summary>
    public enum ToolKind
    {
        /// <summary>Click to select, Shift+click to add or remove, Del to delete.</summary>
        Select,

        /// <summary>Drag objects over the ground, or the gizmo's arrows for one axis.</summary>
        Move,

        /// <summary>Drag the gizmo's rings to turn the selection.</summary>
        Rotate,

        /// <summary>Drag the gizmo's cubes to resize the selection.</summary>
        Scale,

        /// <summary>Drag to pan the camera.</summary>
        Hand,

        /// <summary>Click on a surface to place the selected palette object.</summary>
        Place,

        /// <summary>Paint on the terrain to raise, lower, flatten or smooth it.</summary>
        Sculpt,

        /// <summary>Click the ground to mark a spot, or drag to mark an area, for the agent ("here").</summary>
        Mark,
    }
}
