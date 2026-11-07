using System;
using System.Linq;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The paint tool's options: the ground being painted (picked in the assets browser's Ground tab), the brush's
    /// radius and strength, and Auto (paint the space by its shape), with or without snow above a snow line.
    /// </summary>
    public sealed class PaintSection : IPanelSection
    {
        const string NoGround = "No ground";

        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly Label _ground;
        readonly BrushSliders _brush;
        readonly Toggle _snow;
        readonly FloatField _snowLine;
        readonly Label _land;

        public PaintSection(VisualElement root, EditorContext context, EditorCommands commands)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));
            _group = PanelElements.Required<VisualElement>(root, "options-paint");
            _ground = PanelElements.Required<Label>(root, "paint-ground");
            _brush = new BrushSliders(root, context, "paint-radius", "paint-strength");
            _snow = PanelElements.Required<Toggle>(root, "paint-snow");
            _snow.RegisterValueChangedCallback(change => _context.AutoSnow.On = change.newValue);
            _snowLine = PanelElements.Required<FloatField>(root, "paint-snow-line");
            _snowLine.RegisterValueChangedCallback(change => _context.AutoSnow.Height = change.newValue);
            _land = PanelElements.Required<Label>(root, "paint-land");
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

            // The height shown is the kept one, clamped; a value being typed isn't the field's until committed.
            var snow = _context.AutoSnow;
            if (_snow.value != snow.On)
                _snow.SetValueWithoutNotify(snow.On);
            if (_snowLine.enabledSelf != snow.On)
                _snowLine.SetEnabled(snow.On);
            if (_snowLine.value != snow.Height)
                _snowLine.SetValueWithoutNotify(snow.Height);
            var highest = _context.HighestLand;
            PanelElements.SetText(_land, highest is float top ? FormattableString.Invariant($"Land to {top:0} m") : "No land");
        }
    }
}
