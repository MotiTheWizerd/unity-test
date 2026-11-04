using System;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The outline and fill tools' own option: whether the rectangle snaps to whole pieces.</summary>
    public sealed class RectangleSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly Toggle _snap;

        public RectangleSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _group = PanelElements.Required<VisualElement>(root, "options-rectangle");
            _snap = PanelElements.Required<Toggle>(root, "rectangle-snap");
            _snap.RegisterValueChangedCallback(e => _context.RectangleSnapToPieces = e.newValue);
        }

        public void Refresh()
        {
            var tool = _context.CurrentTool;
            var shown = tool == ToolKind.Outline || tool == ToolKind.Fill;
            PanelElements.SetVisible(_group, shown);
            if (shown && _snap.value != _context.RectangleSnapToPieces)
                _snap.SetValueWithoutNotify(_context.RectangleSnapToPieces);
        }
    }
}
