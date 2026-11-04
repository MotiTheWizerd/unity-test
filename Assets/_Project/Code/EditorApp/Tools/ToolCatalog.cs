using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace Forge.EditorApp.Tools
{
    /// <summary>The editor's tools, in strip order: the one table of names, keys and hints.</summary>
    public static class ToolCatalog
    {
        const int SelectGroup = 0;
        const int ViewGroup = 1;
        const int CreateGroup = 2;

        public static IReadOnlyList<ToolDefinition> All { get; } = new[]
        {
            new ToolDefinition(ToolKind.Select, "Select", Key.Q, SelectGroup, editsWorld: false,
                "Click objects to select them; Shift+click adds or removes. Del deletes."),
            new ToolDefinition(ToolKind.Move, "Move", Key.W, SelectGroup, editsWorld: true,
                "Drag objects over the ground; the arrows move along one axis. Ctrl snaps."),
            new ToolDefinition(ToolKind.Rotate, "Rotate", Key.E, SelectGroup, editsWorld: true,
                "Drag a ring to turn the selection. Ctrl snaps."),
            new ToolDefinition(ToolKind.Scale, "Scale", Key.R, SelectGroup, editsWorld: true,
                "Drag a cube to resize the selection; the middle one resizes it evenly. Ctrl snaps."),
            new ToolDefinition(ToolKind.Hand, "Hand", Key.H, ViewGroup, editsWorld: false,
                "Drag to pan the view. Hold Space for the hand in any tool."),
            new ToolDefinition(ToolKind.Place, "Place", Key.P, CreateGroup, editsWorld: true,
                "Click to place the palette's object; drag to paint a row, Shift for a straight one. [ ] turn it.",
                laysPaletteObject: true),
            new ToolDefinition(ToolKind.Outline, "Outline", Key.O, CreateGroup, editsWorld: true,
                "Drag a rectangle to put the palette's object around it: walls, fences, hedges. Shift: square, [ ] turn it.",
                laysPaletteObject: true),
            new ToolDefinition(ToolKind.Fill, "Fill", Key.G, CreateGroup, editsWorld: true,
                "Drag a rectangle to cover it with the palette's object: floors, paving, fields. Shift: square, [ ] turn it.",
                laysPaletteObject: true),
            new ToolDefinition(ToolKind.Sculpt, "Sculpt", Key.B, CreateGroup, editsWorld: true,
                "Hold left mouse on the terrain to shape it. [ ] brush size."),
            new ToolDefinition(ToolKind.Mark, "Mark", Key.M, CreateGroup, editsWorld: false,
                "Click the ground for a spot, or drag for an area, to show the agent \"here\". Del clears it."),
        };

        static readonly Dictionary<ToolKind, ToolDefinition> ByKind = All.ToDictionary(tool => tool.Kind);

        public static ToolDefinition Of(ToolKind kind) =>
            ByKind.TryGetValue(kind, out var tool) ? tool : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such tool.");
    }
}
