using System;
using UnityEngine.InputSystem;

namespace Forge.EditorApp.Tools
{
    /// <summary>
    /// What the editor shows and binds for one tool: its name, shortcut and hint, and whether it changes the world.
    /// Defined once in <see cref="ToolCatalog"/>; the tool strip, the shortcuts and the status line read it.
    /// </summary>
    public sealed class ToolDefinition
    {
        public ToolDefinition(ToolKind kind, string name, string icon, Key key, int group, bool editsWorld, string hint, bool laysPaletteObject = false)
        {
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Icon = icon ?? throw new ArgumentNullException(nameof(icon));
            Key = key;
            Group = group;
            EditsWorld = editsWorld;
            Hint = hint ?? throw new ArgumentNullException(nameof(hint));
            LaysPaletteObject = laysPaletteObject;
        }

        public ToolKind Kind { get; }

        /// <summary>Short name for the tool strip and the options bar, e.g. "Move".</summary>
        public string Name { get; }

        /// <summary>The icon's name in the icon set (UI/Icons, styled by Theme/ForgeIcons.uss), e.g. "move".</summary>
        public string Icon { get; }

        /// <summary>The key that picks the tool.</summary>
        // SHORTCUT: a fixed key, not an Input Actions binding; both move to the Input Actions asset when
        // controls become rebindable (see ARCHITECTURE.md).
        public Key Key { get; }

        /// <summary>The key as the user reads it, e.g. "W".</summary>
        public string KeyLabel => Key.ToString();

        /// <summary>Tools of one group sit together on the strip (select and transform, view, create).</summary>
        public int Group { get; }

        /// <summary>
        /// Whether the tool changes the world. Such tools rest while the agent's build is open; the others
        /// (select, hand, mark) keep working.
        /// </summary>
        public bool EditsWorld { get; }

        /// <summary>One sentence on how to use it: the strip's tooltip and the options bar's hint.</summary>
        public string Hint { get; }

        /// <summary>
        /// Whether the tool lays the palette's chosen object (place, outline): it needs one, and picking an object in
        /// the palette keeps it instead of switching to the place tool.
        /// </summary>
        public bool LaysPaletteObject { get; }
    }
}
