using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The paint tool's options: which ground layer to paint, and the brush's radius and strength.</summary>
    public sealed class PaintSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly VisualElement _layersRow;
        readonly BrushSliders _brush;
        IReadOnlyList<GroundLayerRecord> _shownLayers;
        ChoiceRow<RecordId> _layers;

        public PaintSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _group = PanelElements.Required<VisualElement>(root, "options-paint");
            _layersRow = PanelElements.Required<VisualElement>(root, "paint-layers");
            _brush = new BrushSliders(root, context, "paint-radius", "paint-strength");
        }

        public void Refresh()
        {
            var painting = _context.CurrentTool == ToolKind.Paint;
            PanelElements.SetVisible(_group, painting);
            if (!painting)
                return;

            // The layers change only with the world (a mod, an undo); the row is rebuilt then.
            var layers = _context.GroundLayers;
            if (!ReferenceEquals(layers, _shownLayers))
            {
                _shownLayers = layers;
                var names = layers.ToDictionary(layer => layer.Id, layer => layer.DisplayName);
                _layers = new ChoiceRow<RecordId>(_layersRow, layers.Select(layer => layer.Id), id => names[id], id => _context.PaintLayer = id);
            }

            _layers.Refresh(_context.PaintLayer);
            _brush.Refresh();
        }
    }
}
