using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>Draws the selection box being dragged (<see cref="EditorContext.SelectionBox"/>) over the view.</summary>
    public sealed class SelectionBoxView : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _box;

        public SelectionBoxView(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _box = PanelElements.Required<VisualElement>(root, "selection-box");
            _box.pickingMode = PickingMode.Ignore;
        }

        public void Refresh()
        {
            var box = _context.SelectionBox;
            PanelElements.SetVisible(_box, box.HasValue);
            var panel = _box.panel;
            if (!box.HasValue || panel == null)
                return;

            // Screen pixels have their origin bottom-left, the panel top-left (and may be scaled).
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(box.Value.Min.X, Screen.height - box.Value.Max.Y));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(box.Value.Max.X, Screen.height - box.Value.Min.Y));
            _box.style.left = topLeft.x;
            _box.style.top = topLeft.y;
            _box.style.width = bottomRight.x - topLeft.x;
            _box.style.height = bottomRight.y - topLeft.y;
        }
    }
}
