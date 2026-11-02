using System;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>Move / Rotate / Scale modes, the selection count, frame and drop to ground.</summary>
    public sealed class TransformSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly ChoiceRow<TransformMode> _modes;
        readonly Label _selection;
        readonly Button _frame;
        readonly Button _drop;

        public TransformSection(VisualElement root, EditorContext context, EditorCommands commands)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));

            _modes = new ChoiceRow<TransformMode>(
                PanelElements.Required<VisualElement>(root, "transform-modes"),
                mode => $"{mode} ({ShortcutOf(mode)})",
                context.SelectTransformMode);
            _selection = PanelElements.Required<Label>(root, "selection-count");
            _frame = PanelElements.Required<Button>(root, "frame-selection");
            _frame.clicked += commands.FrameSelection;
            _drop = PanelElements.Required<Button>(root, "drop-to-ground");
            _drop.clicked += commands.DropSelectionToGround;
        }

        public void Refresh()
        {
            _modes.Refresh(_context.ActiveTool == ToolKind.Select ? _context.TransformMode : (TransformMode?)null);
            PanelElements.SetText(_selection, $"Selected: {_context.Selection.Count}");
            var any = !_context.Selection.IsEmpty;
            _frame.SetEnabled(any);
            _drop.SetEnabled(any);
        }

        // SHORTCUT: mirrors the keys hard-coded in EditorController; both move to the
        // Input Actions asset when controls become rebindable (see ARCHITECTURE.md).
        static string ShortcutOf(TransformMode mode)
        {
            switch (mode)
            {
                case TransformMode.Move: return "W";
                case TransformMode.Rotate: return "E";
                case TransformMode.Scale: return "R";
                default: return "";
            }
        }
    }
}
