using System;
using Forge.EditorApp.Tools;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The select and transform tools' options: the selection count, frame and drop to ground.</summary>
    public sealed class SelectionSection : IPanelSection
    {
        readonly EditorContext _context;
        readonly VisualElement _group;
        readonly Label _selection;
        readonly Button _frame;
        readonly Button _drop;

        public SelectionSection(VisualElement root, EditorContext context, EditorCommands commands)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));

            _group = PanelElements.Required<VisualElement>(root, "options-selection");
            _selection = PanelElements.Required<Label>(root, "selection-count");
            _frame = PanelElements.Required<Button>(root, "frame-selection");
            _frame.clicked += commands.FrameSelection;
            _drop = PanelElements.Required<Button>(root, "drop-to-ground");
            _drop.clicked += commands.DropSelectionToGround;
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
        }

        static bool WorksOnSelection(ToolKind tool) =>
            tool == ToolKind.Select || tool == ToolKind.Move || tool == ToolKind.Rotate || tool == ToolKind.Scale;
    }
}
