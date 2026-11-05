using System;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The options bar across the top of the view. Its first row is global (the space picker, <see cref="SpaceSection"/>);
    /// its second names the current tool and holds that tool's settings, each as its own section inside it
    /// (<see cref="SelectionSection"/>, <see cref="PlacementSection"/>, <see cref="SculptSection"/>), shown while that
    /// tool is current. The tool's hint is on the status bar. Rests with the tool while the agent's build is open.
    /// </summary>
    public sealed class ToolOptionsBar : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _bar;
        readonly Label _name;

        public ToolOptionsBar(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _bar = PanelElements.Required<VisualElement>(root, "tool-options");
            _name = PanelElements.Required<Label>(root, "tool-options-name");
        }

        public void Refresh()
        {
            var tool = ToolCatalog.Of(_context.CurrentTool);
            PanelElements.SetText(_name, tool.Name.ToUpperInvariant());
            var usable = !(_context.IsDraftOpen && tool.EditsWorld);
            if (_bar.enabledSelf != usable)
                _bar.SetEnabled(usable);
        }
    }
}
