using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace Forge.EditorApp.Tools
{
    /// <summary>The editor's tools, in strip order: the one table of names, icons, keys and hints.</summary>
    public static class ToolCatalog
    {
        const int SelectGroup = 0;
        const int ViewGroup = 1;
        const int CreateGroup = 2;

        public static IReadOnlyList<ToolDefinition> All { get; } = new[]
        {
            new ToolDefinition(ToolKind.Select, "Select", "mouse-pointer-2", Key.Q, SelectGroup, editsWorld: false,
                "Click objects to select them; Shift+click adds or removes. Del deletes."),
            new ToolDefinition(ToolKind.Move, "Move", "move", Key.W, SelectGroup, editsWorld: true,
                "Drag objects over the ground; the arrows move along one axis. Ctrl snaps."),
            new ToolDefinition(ToolKind.Rotate, "Rotate", "rotate-3d", Key.E, SelectGroup, editsWorld: true,
                "Drag a ring to turn the selection. Ctrl snaps."),
            new ToolDefinition(ToolKind.Scale, "Scale", "scaling", Key.R, SelectGroup, editsWorld: true,
                "Drag a cube to resize the selection; the middle one resizes it evenly. Ctrl snaps."),
            new ToolDefinition(ToolKind.Hand, "Hand", "hand", Key.H, ViewGroup, editsWorld: false,
                "Drag to pan the view. Hold Space for the hand in any tool."),
            new ToolDefinition(ToolKind.Place, "Place", "box", Key.P, CreateGroup, editsWorld: true,
                "Click to place the palette's object; drag to paint a row, Shift for a straight one. [ ] turn it.",
                laysPaletteObject: true),
            new ToolDefinition(ToolKind.Outline, "Outline", "square-dashed", Key.O, CreateGroup, editsWorld: true,
                "Drag a rectangle to put the palette's object around it: walls, fences, hedges. Shift: square, [ ] turn it.",
                laysPaletteObject: true),
            new ToolDefinition(ToolKind.Fill, "Fill", "grid-2x2", Key.G, CreateGroup, editsWorld: true,
                "Drag a rectangle to cover it with the palette's object: floors, paving, fields. Shift: square, [ ] turn it.",
                laysPaletteObject: true),
            new ToolDefinition(ToolKind.Sculpt, "Sculpt", "mountain", Key.B, CreateGroup, editsWorld: true,
                "Hold left mouse on the terrain to shape it. [ ] brush size."),
            new ToolDefinition(ToolKind.Water, "Water", "droplets", Key.L, CreateGroup, editsWorld: true,
                "Click a hollow to fill it with a lake; drag up or down to set the level, Ctrl in half meters. Click a lake to change it, Del removes it."),
            new ToolDefinition(ToolKind.Mark, "Mark", "map-pin", Key.M, CreateGroup, editsWorld: false,
                "Click the ground for a spot, or drag for an area, to show the agent \"here\". Del clears it."),
        };

        static readonly Dictionary<ToolKind, ToolDefinition> ByKind = All.ToDictionary(tool => tool.Kind);

        public static ToolDefinition Of(ToolKind kind) =>
            ByKind.TryGetValue(kind, out var tool) ? tool : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such tool.");
    }
}
