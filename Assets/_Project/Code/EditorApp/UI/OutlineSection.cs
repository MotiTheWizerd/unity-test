using System;
using Forge.EditorApp.Tools;
using Forge.Editing.Placement;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The outline tool's own options: around or over the rectangle, and whether it snaps to whole pieces.</summary>
    public sealed class OutlineSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly ChoiceRow<RectPattern> _patterns;
        readonly Toggle _snap;

        public OutlineSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _group = PanelElements.Required<VisualElement>(root, "options-outline");
            _patterns = new ChoiceRow<RectPattern>(
                PanelElements.Required<VisualElement>(root, "outline-patterns"),
                pattern => pattern.ToString(),
                pattern => _context.OutlinePattern = pattern);
            _snap = PanelElements.Required<Toggle>(root, "outline-snap");
            _snap.RegisterValueChangedCallback(e => _context.OutlineSnapToPieces = e.newValue);
        }

        public void Refresh()
        {
            var outlining = _context.CurrentTool == ToolKind.Outline;
            PanelElements.SetVisible(_group, outlining);
            if (!outlining)
                return;

            _patterns.Refresh(_context.OutlinePattern);
            if (_snap.value != _context.OutlineSnapToPieces)
                _snap.SetValueWithoutNotify(_context.OutlineSnapToPieces);
        }
    }
}
