using System;
using Forge.Editing.Terrain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>A radius and a strength slider bound to the shared terrain brush (sculpt and paint use the same one).</summary>
    public sealed class BrushSliders
    {
        readonly EditorContext _context;
        readonly Slider _radius;
        readonly Slider _strength;

        public BrushSliders(VisualElement root, EditorContext context, string radiusName, string strengthName)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _radius = PanelElements.Required<Slider>(root, radiusName);
            _radius.lowValue = BrushShape.MinRadius;
            _radius.highValue = BrushShape.MaxRadius;
            _radius.RegisterValueChangedCallback(e => _context.BrushRadius = e.newValue);

            _strength = PanelElements.Required<Slider>(root, strengthName);
            _strength.lowValue = EditorContext.MinBrushStrength;
            _strength.highValue = 1f;
            _strength.RegisterValueChangedCallback(e => _context.BrushStrength = e.newValue);
        }

        /// <summary>Mirrors the brush, which keys ([ ]) change too, without re-raising the change events.</summary>
        public void Refresh()
        {
            if (!Mathf.Approximately(_radius.value, _context.BrushRadius))
                _radius.SetValueWithoutNotify(_context.BrushRadius);
            if (!Mathf.Approximately(_strength.value, _context.BrushStrength))
                _strength.SetValueWithoutNotify(_context.BrushStrength);
        }
    }
}
