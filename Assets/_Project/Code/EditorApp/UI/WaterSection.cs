using System;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The Water tool's options: the current lake (last made or clicked), its level to type in, and Remove.</summary>
    public sealed class WaterSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly Label _lake;
        readonly FloatField _level;
        readonly Button _remove;

        public WaterSection(VisualElement root, EditorContext context, EditorCommands commands)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));

            _group = PanelElements.Required<VisualElement>(root, "options-water");
            _lake = PanelElements.Required<Label>(root, "water-lake");
            _level = PanelElements.Required<FloatField>(root, "water-level");
            _level.RegisterValueChangedCallback(change => commands.SetCurrentLakeLevel(change.newValue));
            _remove = PanelElements.Required<Button>(root, "water-remove");
            _remove.clicked += commands.RemoveCurrentLake;
        }

        // Mirrors the lake's record, which undo and the agent change too; a value being typed isn't the field's value
        // until it is committed (the field is delayed), so it is never overwritten mid-edit.
        public void Refresh()
        {
            var shown = _context.CurrentTool == ToolKind.Water;
            PanelElements.SetVisible(_group, shown);
            if (!shown)
                return;

            var lake = _context.CurrentLakeRecord;
            PanelElements.SetText(_lake, lake != null ? "Lake" : "No lake picked");
            if (_level.enabledSelf != (lake != null))
            {
                _level.SetEnabled(lake != null);
                _remove.SetEnabled(lake != null);
            }

            if (lake != null && _level.value != lake.Level)
                _level.SetValueWithoutNotify(lake.Level);
        }
    }
}
