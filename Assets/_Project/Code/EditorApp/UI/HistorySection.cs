using System;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>Undo / redo with the next step's description, and save.</summary>
    public sealed class HistorySection : IPanelSection
    {
        readonly EditorContext _context;
        readonly Button _undo;
        readonly Button _redo;
        readonly Label _description;
        readonly Button _save;

        public HistorySection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _undo = PanelElements.Required<Button>(root, "undo");
            _undo.clicked += context.Undo;
            _redo = PanelElements.Required<Button>(root, "redo");
            _redo.clicked += context.Redo;
            _description = PanelElements.Required<Label>(root, "undo-description");
            _save = PanelElements.Required<Button>(root, "save");
            _save.clicked += () => context.Save();
        }

        public void Refresh()
        {
            var session = _context.Session;
            _undo.SetEnabled(session.CanUndo);
            _redo.SetEnabled(session.CanRedo);
            PanelElements.SetVisible(_description, session.CanUndo);
            if (session.CanUndo)
                PanelElements.SetText(_description, $"Undo: {session.UndoDescription}");
            PanelElements.SetText(_save, session.IsDirty ? "Save (Ctrl+S)  ●" : "Save (Ctrl+S)");
        }
    }
}
