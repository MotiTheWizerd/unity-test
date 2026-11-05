using System;
using System.Collections.Generic;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The tool strip: an icon button per tool from <see cref="ToolCatalog"/>, grouped as the catalog groups them,
    /// with the tool's name and key as a tooltip. The current tool is highlighted (the hand while Space holds it);
    /// tools that change the world are greyed out while the agent's build is open.
    /// </summary>
    public sealed class ToolStrip : IPanelSection
    {
        const string ToolClass = "forge-tool";
        const string GapClass = "forge-tool-strip__gap";

        readonly EditorContext _context;
        readonly Dictionary<ToolKind, Button> _buttons = new Dictionary<ToolKind, Button>();

        public ToolStrip(VisualElement root, EditorContext context, TooltipLayer tooltips)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (tooltips == null) throw new ArgumentNullException(nameof(tooltips));
            var strip = PanelElements.Required<VisualElement>(root, "tool-strip");
            strip.Clear();

            int? group = null;
            foreach (var tool in ToolCatalog.All)
            {
                if (group.HasValue && group.Value != tool.Group)
                    strip.Add(Gap());
                group = tool.Group;
                var button = CreateButton(tool);
                tooltips.Attach(button, Tooltip(tool));
                strip.Add(button);
                _buttons.Add(tool.Kind, button);
            }
        }

        public void Refresh()
        {
            var current = _context.CurrentTool;
            var draftOpen = _context.IsDraftOpen;
            foreach (var pair in _buttons)
            {
                PanelElements.SetActive(pair.Value, pair.Key == current);
                var usable = !(draftOpen && ToolCatalog.Of(pair.Key).EditsWorld);
                if (pair.Value.enabledSelf != usable)
                    pair.Value.SetEnabled(usable);
            }
        }

        Button CreateButton(ToolDefinition tool)
        {
            var kind = tool.Kind;
            var button = new Button(() => _context.SelectTool(kind));
            button.AddToClassList(ToolClass);
            button.Add(PanelElements.Icon(tool.Icon));
            return button;
        }

        static string Tooltip(ToolDefinition tool) => $"{tool.Name}  <color=#9EA4AE>{tool.KeyLabel}</color>";

        static VisualElement Gap()
        {
            var gap = new VisualElement();
            gap.AddToClassList(GapClass);
            return gap;
        }
    }
}
