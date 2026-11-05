using System;
using Forge.Editing.Terrain;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The sculpt tool's options: brush mode, radius and strength, and whether objects follow the ground.</summary>
    public sealed class SculptSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly ChoiceRow<BrushMode> _modes;
        readonly BrushSliders _brush;
        readonly Toggle _follow;

        public SculptSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _group = PanelElements.Required<VisualElement>(root, "options-sculpt");
            _modes = new ChoiceRow<BrushMode>(
                PanelElements.Required<VisualElement>(root, "brush-modes"),
                mode => mode.ToString(),
                mode => _context.BrushMode = mode);
            _brush = new BrushSliders(root, context, "brush-radius", "brush-strength");

            _follow = PanelElements.Required<Toggle>(root, "objects-follow");
            _follow.RegisterValueChangedCallback(e => _context.ObjectsFollowTerrain = e.newValue);
        }

        public void Refresh()
        {
            var sculpting = _context.CurrentTool == ToolKind.Sculpt;
            PanelElements.SetVisible(_group, sculpting);
            if (!sculpting)
                return;

            _modes.Refresh(_context.BrushMode);
            _brush.Refresh();
            if (_follow.value != _context.ObjectsFollowTerrain)
                _follow.SetValueWithoutNotify(_context.ObjectsFollowTerrain);
        }
    }
}
