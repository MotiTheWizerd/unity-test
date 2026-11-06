using System;
using System.IO;
using Forge.Data.Json;
using Forge.Editing.State;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Keeps the editor's state between sessions, in two files under <c>&lt;persistentDataPath&gt;/Editor</c>: the user's
    /// preferences (<c>prefs.json</c>, every world) and where the user left off in this world
    /// (<c>Worlds/&lt;edit plugin id&gt;.json</c>). Both are read once at startup; then, once a second, the state is captured
    /// and written if it changed, and once more when the editor closes. Nothing here is in a plugin or undone.
    /// </summary>
    public sealed class EditorStateKeeper
    {
        public const string FolderName = "Editor";
        const float SaveInterval = 1f;

        readonly JsonDocumentFile<EditorPrefs> _prefsFile;
        readonly JsonDocumentFile<WorldViewState> _worldFile;
        readonly Action<EditorPrefs, WorldViewState> _capture;
        float _sinceSave;
        bool _warned;

        /// <param name="root">The folder that holds the editor's folder (<c>Application.persistentDataPath</c>).</param>
        /// <param name="worldId">The world's edit plugin id, which names its file.</param>
        /// <param name="capture">Writes the live state into the kept documents (the loaded ones, so values no one captures survive).</param>
        public EditorStateKeeper(string root, string worldId, Action<EditorPrefs, WorldViewState> capture)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrEmpty(worldId))
                throw new ArgumentException("The world needs an id.", nameof(worldId));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));

            var folder = Path.Combine(root, FolderName);
            _prefsFile = new JsonDocumentFile<EditorPrefs>(Path.Combine(folder, "prefs.json"));
            _worldFile = new JsonDocumentFile<WorldViewState>(Path.Combine(folder, "Worlds", worldId + ".json"));
            Prefs = Load(_prefsFile);
            World = Load(_worldFile);
        }

        /// <summary>The user's preferences, as read at startup and captured since.</summary>
        public EditorPrefs Prefs { get; }

        /// <summary>Where the user left off in this world, as read at startup and captured since.</summary>
        public WorldViewState World { get; }

        /// <summary>Saves once <see cref="SaveInterval"/> has passed since the last save.</summary>
        public void Tick(float realSeconds)
        {
            _sinceSave += realSeconds;
            if (_sinceSave < SaveInterval)
                return;
            _sinceSave = 0f;
            Save();
        }

        /// <summary>Captures the state and writes each file whose content changed. A failed write is reported once, not thrown.</summary>
        public void Save()
        {
            _capture(Prefs, World);
            try
            {
                _prefsFile.Save(Prefs);
                _worldFile.Save(World);
                _warned = false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (_warned)
                    return;
                _warned = true;
                Debug.LogWarning($"[Forge] The editor's state could not be saved: {ex.Message}");
            }
        }

        static T Load<T>(JsonDocumentFile<T> file) where T : class, new()
        {
            var document = file.Load(out var problem);
            if (problem != null)
                Debug.LogWarning($"[Forge] {problem}");
            return document;
        }
    }
}
