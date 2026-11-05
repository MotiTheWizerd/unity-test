using System;
using System.Linq;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The paint tool's options: the ground being painted (picked in the assets browser's Ground tab), the brush's
    /// radius and strength, and Auto (paint the space by its shape).
    /// </summary>
    public sealed class PaintSection : IPanelSection
    {
        const string NoGround = "No ground";

        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly Label _ground;
        readonly BrushSliders _brush;

        public PaintSection(VisualElement root, EditorContext context, EditorCommands commands)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));
            _group = PanelElements.Required<VisualElement>(root, "options-paint");
            _ground = PanelElements.Required<Label>(root, "paint-ground");
            _brush = new BrushSliders(root, context, "paint-radius", "paint-strength");
            PanelElements.Required<Button>(root, "paint-auto").clicked += commands.AutoPaintSpace;
        }

        public void Refresh()
        {
            var painting = _context.CurrentTool == ToolKind.Paint;
            PanelElements.SetVisible(_group, painting);
            if (!painting)
                return;

            var layer = _context.PaintLayer;
            var record = layer.HasValue ? _context.GroundLayers.FirstOrDefault(l => l.Id == layer.Value) : null;
            PanelElements.SetText(_ground, record?.DisplayName ?? NoGround);
            _brush.Refresh();
        }
    }
}
