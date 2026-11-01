using System;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.EditorApp.Tools;
using Forge.Editing;
using Forge.Editing.Terrain;

namespace Forge.EditorApp
{
    /// <summary>
    /// Shared state of the running editor: the session, selection, active tool,
    /// palette choice, where the active plugin is saved, and the last status
    /// message. Plain C# so tools and UI stay decoupled from each other.
    /// </summary>
    public sealed class EditorContext
    {
        readonly PluginFileStore _activeStore;
        float _brushRadius = 8f;
        float _brushStrength = 0.5f;

        public EditorContext(EditSession session, PluginFileStore activeStore)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _activeStore = activeStore ?? throw new ArgumentNullException(nameof(activeStore));
            SelectedBaseId = session.World.Records.OfType<BaseObjectRecord>().Select(r => r.Id).OrderBy(id => id).FirstOrDefault();
            Status = $"Loaded {session.World.LoadOrder.Count} plugin(s).";
        }

        public EditSession Session { get; }

        public Selection Selection { get; } = new Selection();

        public ToolKind ActiveTool { get; set; } = ToolKind.Select;

        public string ActivePluginPath => _activeStore.PathFor(Session.ActivePluginId);

        /// <summary>Base object the place tool creates.</summary>
        public RecordId SelectedBaseId { get; set; }

        public BrushMode BrushMode { get; set; } = BrushMode.Raise;

        /// <summary>Brush radius in meters; clamped to the brush's valid range.</summary>
        public float BrushRadius
        {
            get => _brushRadius;
            set => _brushRadius = Math.Max(BrushSettings.MinRadius, Math.Min(BrushSettings.MaxRadius, value));
        }

        /// <summary>Brush strength in (0, 1]; clamped.</summary>
        public float BrushStrength
        {
            get => _brushStrength;
            set => _brushStrength = Math.Max(0.05f, Math.Min(1f, value));
        }

        public BrushSettings CurrentBrush() => new BrushSettings(BrushMode, BrushRadius, BrushStrength);

        public string Status { get; private set; }

        public void Report(string message) => Status = message;

        /// <summary>
        /// Runs an edit; rejected edits (invalid data) are reported instead of
        /// thrown, so tools and UI never crash on a bad action.
        /// </summary>
        public bool TryEdit(string action, Action edit)
        {
            try
            {
                edit();
                return true;
            }
            catch (Exception ex) when (ex is DataValidationException || ex is ArgumentException || ex is InvalidOperationException)
            {
                Report($"{action} failed: {ex.Message}");
                UnityEngine.Debug.LogWarning($"[Forge] {action} failed: {ex.Message}");
                return false;
            }
        }

        public void Undo()
        {
            var description = Session.UndoDescription;
            Report(Session.Undo() ? $"Undid: {description}" : "Nothing to undo");
        }

        public void Redo()
        {
            var description = Session.RedoDescription;
            Report(Session.Redo() ? $"Redid: {description}" : "Nothing to redo");
        }

        /// <summary>Saves the active plugin; failures are reported, not thrown, so any UI can call it.</summary>
        public bool Save()
        {
            try
            {
                _activeStore.Save(Session.BuildActivePlugin());
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is DataValidationException)
            {
                Report($"Save failed: {ex.Message}");
                return false;
            }

            Session.MarkSaved();
            Report($"Saved {Session.ActivePluginId} to {ActivePluginPath}");
            return true;
        }

        public string DisplayNameOf(RecordId id) =>
            Session.World.Records.TryGet(id, out var record) && record is BaseObjectRecord baseObject
                ? baseObject.DisplayName
                : id.ToString();
    }
}
