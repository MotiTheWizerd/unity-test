using System;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Editing;

namespace Forge.EditorApp
{
    /// <summary>
    /// Shared state of the running editor: the session, where the active plugin
    /// is saved, the current palette selection and the last status message.
    /// Plain C# so tools and UI stay decoupled from each other.
    /// </summary>
    public sealed class EditorContext
    {
        readonly PluginFileStore _activeStore;

        public EditorContext(EditSession session, PluginFileStore activeStore)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _activeStore = activeStore ?? throw new ArgumentNullException(nameof(activeStore));
            SelectedBaseId = session.World.Records.OfType<BaseObjectRecord>().Select(r => r.Id).OrderBy(id => id).FirstOrDefault();
            Status = $"Loaded {session.World.LoadOrder.Count} plugin(s).";
        }

        public EditSession Session { get; }

        public string ActivePluginPath => _activeStore.PathFor(Session.ActivePluginId);

        public RecordId SelectedBaseId { get; set; }

        public string Status { get; private set; }

        public void Report(string message) => Status = message;

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
    }
}
