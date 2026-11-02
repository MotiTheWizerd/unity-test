using System;
using Forge.Editing.Terrain;
using Forge.EditorApp.Tools;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The sculpt tool button and, while sculpting, the brush options.</summary>
    public sealed class SculptSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly Button _tool;
        readonly VisualElement _options;
        readonly ChoiceRow<BrushMode> _modes;
        readonly Slider _radius;
        readonly Slider _strength;
        readonly Toggle _follow;

        public SculptSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _tool = PanelElements.Required<Button>(root, "sculpt-tool");
            _tool.clicked += () => _context.ActiveTool = ToolKind.Sculpt;
            _options = PanelElements.Required<VisualElement>(root, "sculpt-options");
            _modes = new ChoiceRow<BrushMode>(
                PanelElements.Required<VisualElement>(root, "brush-modes"),
                mode => mode.ToString(),
                mode => _context.BrushMode = mode);

            _radius = PanelElements.Required<Slider>(root, "brush-radius");
            _radius.lowValue = BrushSettings.MinRadius;
            _radius.highValue = BrushSettings.MaxRadius;
            _radius.RegisterValueChangedCallback(e => _context.BrushRadius = e.newValue);

            _strength = PanelElements.Required<Slider>(root, "brush-strength");
            _strength.lowValue = EditorContext.MinBrushStrength;
            _strength.highValue = 1f;
            _strength.RegisterValueChangedCallback(e => _context.BrushStrength = e.newValue);

            _follow = PanelElements.Required<Toggle>(root, "objects-follow");
            _follow.RegisterValueChangedCallback(e => _context.ObjectsFollowTerrain = e.newValue);
        }

        public void Refresh()
        {
            var sculpting = _context.ActiveTool == ToolKind.Sculpt;
            PanelElements.SetActive(_tool, sculpting);
            PanelElements.SetVisible(_options, sculpting);
            if (!sculpting)
                return;

            _modes.Refresh(_context.BrushMode);
            // Keys ([ ]) can change the radius too; mirror it without re-raising the change event.
            if (!Mathf.Approximately(_radius.value, _context.BrushRadius))
                _radius.SetValueWithoutNotify(_context.BrushRadius);
            if (!Mathf.Approximately(_strength.value, _context.BrushStrength))
                _strength.SetValueWithoutNotify(_context.BrushStrength);
            if (_follow.value != _context.ObjectsFollowTerrain)
                _follow.SetValueWithoutNotify(_context.ObjectsFollowTerrain);
        }
    }
}
