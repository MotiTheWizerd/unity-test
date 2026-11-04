using System;
using System.Collections.Generic;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The tool strip: a button per tool from <see cref="ToolCatalog"/>, with its name and key, grouped as the catalog
    /// groups them. The current tool is highlighted (the hand while Space holds it); tools that change the world are
    /// greyed out while the agent's build is open.
    /// </summary>
    public sealed class ToolStrip : IPanelSection
    {
        const string ToolClass = "forge-tool";
        const string KeyClass = "forge-tool__key";
        const string GapClass = "forge-tool-strip__gap";

        readonly EditorContext _context;
        readonly Dictionary<ToolKind, Button> _buttons = new Dictionary<ToolKind, Button>();

        public ToolStrip(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            var strip = PanelElements.Required<VisualElement>(root, "tool-strip");
            strip.Clear();

            int? group = null;
            foreach (var tool in ToolCatalog.All)
            {
                if (group.HasValue && group.Value != tool.Group)
                    strip.Add(Gap());
                group = tool.Group;
                var button = CreateButton(tool);
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
            var button = new Button(() => _context.SelectTool(kind)) { tooltip = $"{tool.Name} ({tool.KeyLabel}): {tool.Hint}" };
            button.AddToClassList(ToolClass);
            button.Add(new Label(tool.Name));
            var key = new Label(tool.KeyLabel);
            key.AddToClassList(KeyClass);
            button.Add(key);
            return button;
        }

        static VisualElement Gap()
        {
            var gap = new VisualElement();
            gap.AddToClassList(GapClass);
            return gap;
        }
    }
}
