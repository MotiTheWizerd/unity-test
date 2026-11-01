using System;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Editing;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.EditorApp
{
    /// <summary>
    /// Composition root of the runtime editor: loads plugins, builds the session
    /// and world view, and hands the shared context to tools and UI.
    /// </summary>
    public sealed class EditorAppBootstrap : MonoBehaviour
    {
        const string PluginFolder = "Plugins";

        [SerializeField, Tooltip("Plugin that receives all edits; saved under the user's data folder.")]
        string _activePluginId = "forge.user";

        [SerializeField] EditorController _controller;
        [SerializeField] EditorOverlay _overlay;

        // Runtime-only state; see EditorOverlay for why these are [NonSerialized].
        [NonSerialized] WorldView _view;
        [NonSerialized] EditorContext _context;
        [NonSerialized] SelectionHighlighter _highlighter;

        void Start()
        {
            try
            {
                _context = CreateContext();
                _view = new WorldView(transform, new PrimitiveAssetCatalog());
                _view.Sync(_context.Session.World.Records);
                _highlighter = new SelectionHighlighter(_context.Selection, _view);

                _context.Session.WorldChanged += OnWorldChanged;
                _context.Selection.Changed += _highlighter.Refresh;

                _controller.Initialize(_context, _view);
                _overlay.Initialize(_context);
            }
            catch (Exception ex) when (ex is DataValidationException || ex is IOException)
            {
                Debug.LogError($"[Forge] Failed to start editor: {ex.Message}");
                _overlay.ShowFatal(ex.Message);
            }
        }

        void OnDestroy()
        {
            if (_context != null)
            {
                _context.Session.WorldChanged -= OnWorldChanged;
                _context.Selection.Changed -= _highlighter.Refresh;
            }

            _view?.Dispose();
        }

        // Order matters: rebuild the scene, drop selections of vanished records, then re-tint.
        void OnWorldChanged(ResolvedWorld world)
        {
            _view.Sync(world.Records);
            _context.Selection.Prune(world.Records);
            _highlighter.Refresh();
        }

        EditorContext CreateContext()
        {
            var serializer = new JsonPluginSerializer(RecordTypeRegistry.CreateDefault());
            var baseStore = new PluginFileStore(Path.Combine(Application.streamingAssetsPath, PluginFolder), serializer);
            var userStore = new PluginFileStore(Path.Combine(Application.persistentDataPath, PluginFolder), serializer);

            var masters = baseStore.LoadAll();
            var active = userStore.Exists(_activePluginId)
                ? userStore.Load(_activePluginId)
                : new Plugin(new PluginManifest(_activePluginId, "0.1.0", masters.Select(p => p.Id)), null);

            return new EditorContext(new EditSession(masters, active), userStore);
        }
    }
}
