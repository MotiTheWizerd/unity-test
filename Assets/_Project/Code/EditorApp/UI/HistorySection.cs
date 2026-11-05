using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// Undo, redo and save, as icons in the top bar's global row. The next undo step's description rides on the undo
    /// button's tooltip; unsaved edits mark the save icon (the .is-dirty class).
    /// </summary>
    public sealed class HistorySection : IPanelSection
    {
        public const string DirtyClass = "is-dirty";

        const string UndoTooltip = "Undo (Ctrl+Z)";
        const string SaveTooltip = "Save (Ctrl+S)";
        const string SaveDirtyTooltip = "Save unsaved edits (Ctrl+S)";

        readonly EditorContext _context;
        readonly Button _undo;
        readonly Button _redo;
        readonly Button _save;

        public HistorySection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _undo = PanelElements.Required<Button>(root, "undo");
            _undo.clicked += context.Undo;
            _redo = PanelElements.Required<Button>(root, "redo");
            _redo.clicked += context.Redo;
            _save = PanelElements.Required<Button>(root, "save");
            _save.clicked += () => context.Save();
        }

        public void Refresh()
        {
            var session = _context.Session;
            _undo.SetEnabled(session.CanUndo);
            _undo.tooltip = session.CanUndo ? $"Undo: {session.UndoDescription} (Ctrl+Z)" : UndoTooltip;
            _redo.SetEnabled(session.CanRedo);
            _save.EnableInClassList(DirtyClass, session.IsDirty);
            _save.tooltip = session.IsDirty ? SaveDirtyTooltip : SaveTooltip;
        }
    }
}
