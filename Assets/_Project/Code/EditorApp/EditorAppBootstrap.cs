using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.EditorApp.Diagnostics;
using Forge.EditorApp.UI;
using Forge.Editing;
using Forge.Streaming;
using Forge.World;
using Forge.World.Assets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Forge.EditorApp
{
    /// <summary>
    /// Composition root of the runtime editor: loads the world profile's plugins,
    /// builds the session, world view and streamer, and hands the shared context
    /// to tools and UI.
    /// </summary>
    public sealed class EditorAppBootstrap : MonoBehaviour
    {
        const string UserPluginFolder = "Plugins";

        [SerializeField, Tooltip("Which world to open: base plugin folders and the plugin that receives edits.")]
        WorldProfile _profile;

        [SerializeField, Tooltip("Streaming follows this transform (normally the camera).")]
        Transform _focus;

        [SerializeField, Min(0), Tooltip("Chunks within this distance (in chunks) are loaded.")]
        int _loadRadius = 6;

        [SerializeField, Min(0), Tooltip("Chunks beyond this distance are unloaded; must be at least the load radius.")]
        int _unloadRadius = 8;

        [SerializeField, Min(0.1f), Tooltip("Milliseconds per frame spent building chunks (at least one per frame).")]
        float _frameBudgetMs = 4f;

        [SerializeField, Tooltip("Prefabs for the asset keys of base objects; unlisted keys fall back to primitives.")]
        PrefabCatalog _prefabs;

        [SerializeField] EditorController _controller;
        [SerializeField] EditorPanel _panel;

        // Runtime-only state; see CLAUDE.md (play-mode reload) for why these are [NonSerialized].
        [NonSerialized] WorldView _view;
        [NonSerialized] WorldStreamer _streamer;
        [NonSerialized] EditorContext _context;
        [NonSerialized] SelectionHighlighter _highlighter;
        [NonSerialized] FrameTimeTracker _frames;

        void Start()
        {
            if (_prefabs == null)
            {
                const string message = "EditorApp has no Prefab Catalog assigned (EditorAppBootstrap._prefabs).";
                Debug.LogError($"[Forge] {message}");
                _panel.ShowFatal(message);
                return;
            }

            try
            {
                var clock = Stopwatch.StartNew();
                var serializer = new JsonPluginSerializer(RecordTypeRegistry.CreateDefault());
                var userStore = new PluginFileStore(Path.Combine(Application.persistentDataPath, UserPluginFolder), serializer);
                var masters = LoadMasters(serializer);
                var active = LoadOrCreateActive(userStore, masters);
                var loadMs = Lap(clock);

                _context = new EditorContext(new EditSession(masters, active), userStore);
                var resolveMs = Lap(clock);

                var assets = new PrefabAssetCatalog(_prefabs, new PrimitiveAssetCatalog());
                _view = new WorldView(transform, assets);
                _streamer = new WorldStreamer(_view, new ChunkWindow(_loadRadius, Math.Max(_loadRadius, _unloadRadius)), _frameBudgetMs);
                _view.Sync(_context.Session.World);
                _streamer.LoadAll(_focus.position);
                var viewMs = Lap(clock);

                _frames = new FrameTimeTracker();
                var diagnostics = new EditorDiagnostics(_view, _streamer, _frames);
                diagnostics.RecordStartup(loadMs, resolveMs, viewMs);
                Debug.Log($"[Forge] Opened '{_profile.name}': {_context.Session.World.Records.Count} records, " +
                          $"load {loadMs:0} ms, resolve {resolveMs:0} ms, first view {viewMs:0} ms ({_view.LoadedChunks.Count} chunks, {_view.PresentedCount} presented).");

                _highlighter = new SelectionHighlighter(_context.Selection, _view);
                _context.Session.WorldChanged += OnWorldChanged;
                _context.Selection.Changed += _highlighter.Refresh;

                _controller.Initialize(_context, _view, _panel, assets);
                _panel.Initialize(_context, _controller.Commands, diagnostics);
            }
            catch (Exception ex) when (ex is DataValidationException || ex is IOException)
            {
                Debug.LogError($"[Forge] Failed to start editor: {ex.Message}");
                _panel.ShowFatal(ex.Message);
            }
        }

        void Update()
        {
            if (_streamer == null)
                return;

            _frames.Record(Time.unscaledDeltaTime * 1000.0);
            // Chunks that stream back in must show their selection tint again.
            if (_streamer.Tick(_focus.position))
                _highlighter.Refresh();
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
            _view.Sync(world);
            _context.Selection.Prune(world.Records);
            _highlighter.Refresh();
        }

        IReadOnlyList<Plugin> LoadMasters(IPluginSerializer serializer)
        {
            var masters = new List<Plugin>();
            foreach (var folder in _profile.BaseFolders)
            {
                var directory = Path.Combine(Application.streamingAssetsPath, folder);
                // A missing folder would silently open a smaller world, and the edit
                // plugin created for it would lack masters it needs later.
                if (!Directory.Exists(directory))
                    throw new DirectoryNotFoundException($"World '{_profile.name}' needs the plugin folder '{folder}' under StreamingAssets, which does not exist.");
                masters.AddRange(new PluginFileStore(directory, serializer).LoadAll());
            }

            return masters;
        }

        Plugin LoadOrCreateActive(PluginFileStore userStore, IReadOnlyList<Plugin> masters) =>
            userStore.Exists(_profile.ActivePluginId)
                ? userStore.Load(_profile.ActivePluginId)
                : new Plugin(new PluginManifest(_profile.ActivePluginId, "0.1.0", masters.Select(p => p.Id)), null);

        static double Lap(Stopwatch clock)
        {
            var elapsed = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            return elapsed;
        }
    }
}
