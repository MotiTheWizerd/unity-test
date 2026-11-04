using System;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The tool options bar across the top of the view: the current tool's name and hint. Each tool's settings are
    /// their own section inside it (<see cref="SelectionSection"/>, <see cref="PlacementSection"/>,
    /// <see cref="SculptSection"/>), shown while that tool is current. Rests with the tool while the agent's build is open.
    /// </summary>
    public sealed class ToolOptionsBar : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _bar;
        readonly Label _name;
        readonly Label _hint;

        public ToolOptionsBar(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _bar = PanelElements.Required<VisualElement>(root, "tool-options");
            _name = PanelElements.Required<Label>(root, "tool-options-name");
            _hint = PanelElements.Required<Label>(root, "tool-options-hint");
        }

        public void Refresh()
        {
            var tool = ToolCatalog.Of(_context.CurrentTool);
            PanelElements.SetText(_name, tool.Name.ToUpperInvariant());
            PanelElements.SetText(_hint, tool.Hint);
            var usable = !(_context.IsDraftOpen && tool.EditsWorld);
            if (_bar.enabledSelf != usable)
                _bar.SetEnabled(usable);
        }
    }
}
