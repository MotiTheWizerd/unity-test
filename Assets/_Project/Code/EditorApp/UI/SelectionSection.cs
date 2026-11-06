using System;
using System.Collections.Generic;
using System.Linq;
using Forge.EditorApp.Tools;
using Forge.Editing;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The select and transform tools' options: the selection count, frame and drop to ground, and, while the selection
    /// has material slots, which slot a material ball dresses and a reset to the base look.
    /// </summary>
    public sealed class SelectionSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly EditorCommands _commands;
        readonly VisualElement _group;
        readonly Label _selection;
        readonly Button _frame;
        readonly Button _drop;
        readonly DropdownField _slot;
        readonly Button _resetMaterials;
        IReadOnlyList<string> _slots = Array.Empty<string>();

        public SelectionSection(VisualElement root, EditorContext context, EditorCommands commands)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));

            _group = PanelElements.Required<VisualElement>(root, "options-selection");
            _selection = PanelElements.Required<Label>(root, "selection-count");
            _frame = PanelElements.Required<Button>(root, "frame-selection");
            _frame.clicked += commands.FrameSelection;
            _drop = PanelElements.Required<Button>(root, "drop-to-ground");
            _drop.clicked += commands.DropSelectionToGround;
            _slot = PanelElements.Required<DropdownField>(root, "material-slot");
            _slot.RegisterValueChangedCallback(_ => _context.MaterialSlot = _slot.index >= 0 && _slot.index < _slots.Count ? _slots[_slot.index] : null);
            _resetMaterials = PanelElements.Required<Button>(root, "reset-materials");
            _resetMaterials.clicked += commands.ResetSelectionMaterials;
        }

        public void Refresh()
        {
            var shown = WorksOnSelection(_context.CurrentTool);
            PanelElements.SetVisible(_group, shown);
            if (!shown)
                return;

            PanelElements.SetText(_selection, $"Selected: {_context.Selection.Count}");
            var any = !_context.Selection.IsEmpty;
            _frame.SetEnabled(any);
            _drop.SetEnabled(any);
            ShowSlots(_commands.SelectionSlots());
        }

        void ShowSlots(IReadOnlyList<string> slots)
        {
            var hasSlots = slots.Count > 0;
            PanelElements.SetVisible(_slot, hasSlots);
            PanelElements.SetVisible(_resetMaterials, hasSlots);
            if (!hasSlots)
                return;

            if (!slots.SequenceEqual(_slots))
            {
                _slots = slots;
                _slot.choices = slots.Select(Materials.SlotLabel).ToList();
            }

            var index = IndexOf(_slots, _commands.DressedSlot(_slots));
            if (_slot.index != index)
                _slot.SetValueWithoutNotify(_slot.choices[index]);
        }

        static int IndexOf(IReadOnlyList<string> slots, string slot)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == slot)
                    return i;
            }

            return 0;
        }

        static bool WorksOnSelection(ToolKind tool) =>
            tool == ToolKind.Select || tool == ToolKind.Move || tool == ToolKind.Rotate || tool == ToolKind.Scale;
    }
}
