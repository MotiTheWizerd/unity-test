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
using Forge.Data.Terrain;
using Forge.EditorApp.Agent;
using Forge.EditorApp.Diagnostics;
using Forge.EditorApp.UI;
using Forge.Editing;
using Forge.Streaming;
using Forge.World;
using Forge.World.Assets;
using Forge.World.Sky;
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

        [SerializeField, Tooltip("Material of the sea and the lakes (Materials/Water); without one no water is shown.")]
        Material _water;

        [SerializeField, Tooltip("Material the land is drawn with (Materials/Terrain, URP Terrain Lit).")]
        Material _terrainMaterial;

        [SerializeField, Tooltip("Unlit material the interior floor grid is painted with (Materials/InteriorFloor).")]
        Material _interiorFloorMaterial;

        [SerializeField, Tooltip("How the sky looks (Settings/Sky); without one the scene keeps its own sky and light.")]
        SkyProfile _sky;

        [SerializeField, Tooltip("The scene's directional light, which the sky drives as the sun.")]
        Light _sun;

        [SerializeField, Tooltip("How the world-building agent runs; leave empty for an editor without one.")]
        AgentSettings _agent;

        [SerializeField] EditorController _controller;
        [SerializeField] EditorPanel _panel;

        // Runtime-only state; see CLAUDE.md (play-mode reload) for why these are [NonSerialized].
        [NonSerialized] WorldView _view;
        [NonSerialized] WorldStreamer _streamer;
        [NonSerialized] EditorContext _context;
        [NonSerialized] SelectionHighlighter _highlighter;
        [NonSerialized] FrameTimeTracker _frames;
        [NonSerialized] InteriorFloor _interiorFloor;
        [NonSerialized] WaterView _waterView;
        [NonSerialized] SkyView _skyView;
        [NonSerialized] SkyDirector _skyDirector;
        [NonSerialized] SpaceCameras _spaceCameras;
        [NonSerialized] ThumbnailRenderer _thumbnails;
        [NonSerialized] RecordId _shownSpace;

        void Start()
        {
            if (MissingSetup() is string missing)
            {
                Debug.LogError($"[Forge] {missing}");
                _panel.ShowFatal(missing);
                return;
            }

            try
            {
                var clock = Stopwatch.StartNew();
                var serializer = new JsonPluginSerializer(RecordTypeRegistry.CreateDefault());
                // One budget for the heights of every plugin the world opens.
                var heights = new HeightCache();
                var userStore = new PluginFileStore(Path.Combine(Application.persistentDataPath, UserPluginFolder), serializer, heights);
                var masters = LoadMasters(serializer, heights);
                var active = LoadOrCreateActive(userStore, masters);
                var loadMs = Lap(clock);

                _context = new EditorContext(new EditSession(masters, active), userStore);
                var resolveMs = Lap(clock);

                var assets = new PrefabAssetCatalog(_prefabs, new PrimitiveAssetCatalog());
                _view = new WorldView(transform, assets, _terrainMaterial);
                _streamer = new WorldStreamer(_view, new ChunkWindow(_loadRadius, Math.Max(_loadRadius, _unloadRadius)), _frameBudgetMs);
                _view.Sync(_context.Session.World);
                _interiorFloor = InteriorFloor.Create(transform, _interiorFloorMaterial);
                _waterView = CreateWaterView();
                _skyView = CreateSkyView();
                if (_skyView != null)
                {
                    _skyDirector = new SkyDirector(_skyView, _context);
                    _skyDirector.Tick(0f);
                }
                _spaceCameras = new SpaceCameras();
                _context.SpaceChanged += OnSpaceChanged;
                if (!RecordId.TryParse(_profile.StartSpace, out var startSpace) || !_context.ShowSpace(startSpace))
                    throw new DataValidationException(new[] { $"World '{_profile.name}' has no start space '{_profile.StartSpace}'." });
                var viewMs = Lap(clock);

                _frames = new FrameTimeTracker();
                var diagnostics = new EditorDiagnostics(_view, _streamer, _frames);
                diagnostics.RecordStartup(loadMs, resolveMs, viewMs);
                Debug.Log($"[Forge] Opened '{_profile.name}': {_context.Session.World.Records.Count} records, " +
                          $"load {loadMs:0} ms, resolve {resolveMs:0} ms, first view {viewMs:0} ms ({_view.LoadedChunks.Count} chunks, {_view.PresentedCount} presented).");

                _highlighter = new SelectionHighlighter(_context.Selection, _view);
                _context.Session.WorldChanged += OnWorldChanged;
                _context.Selection.Changed += _highlighter.Refresh;

                _thumbnails = new ThumbnailRenderer(assets, transform);
                _controller.Initialize(_context, _view, _waterView, _panel, assets, _agent);
                _panel.Initialize(_context, _controller.Commands, diagnostics, _thumbnails, _controller.Agent?.Conversation);
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
            _waterView.Follow(_focus.position);
            _skyDirector?.Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            if (_context != null)
            {
                _context.Session.WorldChanged -= OnWorldChanged;
                _context.SpaceChanged -= OnSpaceChanged;
                _context.Selection.Changed -= _highlighter.Refresh;
            }

            _view?.Dispose();
            _waterView?.Dispose();
            _skyView?.Dispose();
            _thumbnails?.Dispose();
        }

        // The camera moves to where it last was in the new space (the scene's own view for the first one), then the
        // space streams in around it at once: a few chunks around the view, the same load as startup.
        void OnSpaceChanged(RecordId space)
        {
            var record = _context.ActiveSpaceRecord;
            if (!_shownSpace.IsDefault)
            {
                _spaceCameras.Remember(_shownSpace, _focus);
                var pose = _spaceCameras.Recall(record);
                if (_focus.TryGetComponent<FlyCamera>(out var flyCamera))
                    flyCamera.Teleport(pose.position, pose.rotation);
                else
                    _focus.SetPositionAndRotation(pose.position, pose.rotation);
            }

            _shownSpace = space;
            _interiorFloor.Show(record.Kind == SpaceKind.Interior);
            _waterView.Sync(_context.Session.World, space);
            _streamer.ShowSpace(space, _focus.position);
            _highlighter?.Refresh();
        }

        // Order matters: rebuild the scene, drop selections of vanished records, then re-tint.
        void OnWorldChanged(ResolvedWorld world)
        {
            // The space on show can vanish (an undone "New space"); the world opens where it started instead.
            if (_context.ActiveSpaceRecord == null && RecordId.TryParse(_profile.StartSpace, out var startSpace))
                _context.ShowSpace(startSpace);
            _view.Sync(world);
            _waterView.Sync(world, _context.ActiveSpace);
            _context.Selection.Prune(world.Records);
            _highlighter.Refresh();
        }

        // What the editor can't open without; optional parts (water, sky, agent) only warn when they are missing.
        string MissingSetup() =>
            _prefabs == null ? "EditorApp has no Prefab Catalog assigned (EditorAppBootstrap._prefabs)."
            : _terrainMaterial == null ? "EditorApp has no terrain material assigned (EditorAppBootstrap._terrainMaterial)."
            : _interiorFloorMaterial == null ? "EditorApp has no interior floor material assigned (EditorAppBootstrap._interiorFloorMaterial)."
            : null;

        // The fog is thick where streaming stops loading land, so the world fades into the sky instead of ending.
        SkyView CreateSkyView()
        {
            if (_sky == null || _sun == null)
            {
                Debug.LogWarning("[Forge] EditorApp has no sky profile or sun (EditorAppBootstrap._sky, _sun); the scene keeps its own sky.");
                return null;
            }

            return new SkyView(_sky, _sun, transform, _loadRadius * WorldGrid.ChunkSize);
        }

        // The sea is as large as the camera can see in every direction, so it reaches the horizon.
        WaterView CreateWaterView()
        {
            if (_water == null)
                Debug.LogWarning("[Forge] EditorApp has no water material (EditorAppBootstrap._water); no water is shown.");

            var reach = _focus.TryGetComponent<Camera>(out var view) ? view.farClipPlane : 1000f;
            return new WaterView(transform, _water, 2f * reach);
        }

        IReadOnlyList<Plugin> LoadMasters(IPluginSerializer serializer, HeightCache heights)
        {
            var masters = new List<Plugin>();
            foreach (var folder in _profile.BaseFolders)
            {
                var directory = Path.Combine(Application.streamingAssetsPath, folder);
                // A missing folder would silently open a smaller world, and the edit
                // plugin created for it would lack masters it needs later.
                if (!Directory.Exists(directory))
                    throw new DirectoryNotFoundException($"World '{_profile.name}' needs the plugin folder '{folder}' under StreamingAssets, which does not exist.");
                masters.AddRange(new PluginFileStore(directory, serializer, heights).LoadAll());
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
