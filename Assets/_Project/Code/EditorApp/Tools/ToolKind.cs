namespace Forge.EditorApp.Tools
{
    public enum ToolKind
    {
        /// <summary>Click to select, drag the gizmo to move, Del to delete.</summary>
        Select,

        /// <summary>Click on a surface to place the selected palette object.</summary>
        Place,

        /// <summary>Paint on the terrain to raise, lower, flatten or smooth it.</summary>
        Sculpt,

        /// <summary>Click the ground to mark a spot, or drag to mark an area, for the agent ("here").</summary>
        Mark,
    }
}
