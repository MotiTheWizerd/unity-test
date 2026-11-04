using System;
using Forge.EditorApp.Tools;
using Forge.Editing.Placement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The options of the tools that lay the palette's object (place, outline): the turn ([ ]), random turn and size,
    /// align to slope, and the gap between pieces (plus a painted row's random extra).
    /// </summary>
    public sealed class PlacementSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly Label _rotation;
        readonly Toggle _randomize;
        readonly Toggle _alignToSlope;
        readonly Slider _gap;
        readonly Slider _gapRandom;

        public PlacementSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _group = PanelElements.Required<VisualElement>(root, "options-place");
            _rotation = PanelElements.Required<Label>(root, "placement-rotation");
            PanelElements.Required<Button>(root, "rotate-left").clicked += () => _context.RotatePlacement(-EditorContext.PlacementStep);
            PanelElements.Required<Button>(root, "rotate-right").clicked += () => _context.RotatePlacement(EditorContext.PlacementStep);
            _randomize = PanelElements.Required<Toggle>(root, "placement-randomize");
            _randomize.RegisterValueChangedCallback(e => _context.RandomizePlacement = e.newValue);
            _alignToSlope = PanelElements.Required<Toggle>(root, "placement-align-slope");
            _alignToSlope.RegisterValueChangedCallback(e => _context.AlignPlacementToSlope = e.newValue);
            _gap = GapSlider(root, "placement-gap", value => _context.PlacementGap = value);
            _gapRandom = GapSlider(root, "placement-gap-random", value => _context.PlacementGapRandom = value);
        }

        public void Refresh()
        {
            var tool = _context.CurrentTool;
            var laying = ToolCatalog.Of(tool).LaysPaletteObject;
            PanelElements.SetVisible(_group, laying);
            if (!laying)
                return;

            // A rectangle is laid at regular spacing; only a painted row draws a random extra per gap.
            PanelElements.SetVisible(_gapRandom, tool == ToolKind.Place);

            var roll = _context.PlacementRoll;
            var varies = _context.PlacementCanVary;
            PanelElements.SetText(_rotation, varies && _context.RandomizePlacement
                ? $"Rotation {_context.PlacementYaw:0}° + {roll.YawDegrees:0}°, size ×{roll.Scale:0.00}"
                : $"Rotation {_context.PlacementYaw:0}°");
            _randomize.SetValueWithoutNotify(_context.RandomizePlacement);
            // Objects without variation (houses, walls) are always placed exactly as chosen.
            _randomize.SetEnabled(varies);
            // Upright things (trees, houses) never tilt, so the toggle only matters for fences, walls, rocks.
            _alignToSlope.SetValueWithoutNotify(_context.AlignPlacementToSlope);
            _alignToSlope.SetEnabled(_context.PlacementCanAlignToSlope);
            // Gaps are remembered per object, so switching objects changes what the sliders show.
            Mirror(_gap, _context.PlacementGap);
            Mirror(_gapRandom, _context.PlacementGapRandom);
        }

        static Slider GapSlider(VisualElement root, string name, Action<float> set)
        {
            var slider = PanelElements.Required<Slider>(root, name);
            slider.lowValue = 0f;
            slider.highValue = RowGap.Max;
            slider.RegisterValueChangedCallback(e => set(e.newValue));
            return slider;
        }

        static void Mirror(Slider slider, float value)
        {
            if (!Mathf.Approximately(slider.value, value))
                slider.SetValueWithoutNotify(value);
        }
    }
}
