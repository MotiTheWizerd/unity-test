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
using Forge.EditorApp.Tools;
using Forge.EditorApp.UI;
using Forge.Editing;
using Forge.Editing.Modes;
using Forge.Editing.State;
using Forge.Play;
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

        [SerializeField, Min(0), Tooltip("How many chunks the camera may stray from the scene's origin before the scene is rebased under it, keeping float precision far from the world's origin.")]
        int _rebaseDistance = 8;

        [SerializeField, Min(0), Tooltip("Chunks within this distance (in chunks) are shown as coarse far land, beyond the loaded chunks; the fog reaches this far.")]
        int _farRadius = 16;

        [SerializeField, Tooltip("Prefabs for the asset keys of base objects; unlisted keys fall back to primitives.")]
        PrefabCatalog _prefabs;

        [SerializeField, Tooltip("Material of the sea and the lakes (Materials/Water); without one no water is shown.")]
        Material _water;

        [SerializeField, Tooltip("Material the land is drawn with (Materials/Terrain, URP Terrain Lit).")]
        Material _terrainMaterial;

        [SerializeField, Tooltip("Material the far land is drawn with (Materials/FarTerrain); without one nothing shows beyond the loaded chunks.")]
        Material _farTerrainMaterial;

        [SerializeField, Tooltip("Material grass is drawn with (Materials/Grass, Forge/Grass); its fade end is how far grass shows. Without one no grass grows.")]
        Material _grassMaterial;

        [SerializeField, Tooltip("The terrain layer of each ground layer's asset key (Content/GroundLayerCatalog); without one the land is drawn unpainted.")]
        GroundLayerCatalog _groundLayers;

        [SerializeField, Tooltip("The Unity material of each material record's asset key (Content/MaterialCatalog); without one every object wears its own materials.")]
        MaterialCatalog _materials;

        [SerializeField, Tooltip("What the selection highlight is drawn with over selected objects (Materials/SelectionOverlay).")]
        Material _selectionOverlay;

        [SerializeField, Tooltip("Unlit material the interior floor grid is painted with (Materials/InteriorFloor).")]
        Material _interiorFloorMaterial;

        [SerializeField, Tooltip("How the sky looks (Settings/Sky); without one the scene keeps its own sky and light.")]
        SkyProfile _sky;

        [SerializeField, Tooltip("The scene's directional light, which the sky drives as the sun.")]
        Light _sun;

        [SerializeField, Tooltip("How the world-building agent runs; leave empty for an editor without one.")]
        AgentSettings _agent;

        [SerializeField, Tooltip("How the player is built and controlled (Settings/Player); without one, playing flies the editor camera.")]
        PlayerProfile _player;

        [SerializeField] EditorController _controller;
        [SerializeField] EditorPanel _panel;

        // Runtime-only state; see CLAUDE.md (play-mode reload) for why these are [NonSerialized].
        [NonSerialized] WorldFrame _frame;
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
        [NonSerialized] EditorStateKeeper _state;
        [NonSerialized] ThumbnailRenderer _thumbnails;
        [NonSerialized] GrassView _grass;
        [NonSerialized] EditorGrassSource _grassSource;
        [NonSerialized] RecordId _shownSpace;
        [NonSerialized] Pose? _editingPose;

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
                // One budget each for the terrain (heights and paint) and the objects of every plugin the world opens.
                var terrain = new TerrainCache();
                var objects = new ObjectCache();
                var userStore = new PluginFileStore(Path.Combine(Application.persistentDataPath, UserPluginFolder), serializer, terrain, objects);
                var masters = LoadMasters(serializer, terrain, objects);
                var active = LoadOrCreateActive(userStore, masters);
                var loadMs = Lap(clock);

                _context = new EditorContext(new EditSession(masters, active), userStore) { BaseGroundLook = _groundLayers != null ? _groundLayers.BaseKey : null };
                var resolveMs = Lap(clock);

                var assets = new PrefabAssetCatalog(_prefabs, new PrimitiveAssetCatalog());
                // This object is the world root: the world is built under it at world coordinates, and the frame moves it.
                _frame = new WorldFrame(transform, _rebaseDistance);
                _frame.Shifted += OnSceneShifted;
                // One look-up of the ground's looks, for the near land and the far.
                var ground = CreateGroundLooks();
                var materials = CreateMaterialLooks();
                _view = new WorldView(transform, assets, _terrainMaterial, ground, materials);
                _streamer = new WorldStreamer(_view, new ChunkWindow(_loadRadius, Math.Max(_loadRadius, _unloadRadius)), _frameBudgetMs, CreateFarTerrain(ground));
                _view.Sync(_context.Session.World);
                _grass = CreateGrass(ground);
                _interiorFloor = InteriorFloor.Create(transform, _interiorFloorMaterial);
                _waterView = CreateWaterView();
                _skyView = CreateSkyView();
                if (_skyView != null)
                {
                    _skyDirector = new SkyDirector(_skyView, _context);
                    _skyDirector.Tick(0f);
                }
                _spaceCameras = new SpaceCameras();
                // Where the user left off: the space on show, the camera in each space, the editor's settings.
                _state = new EditorStateKeeper(Application.persistentDataPath, _profile.ActivePluginId, CaptureState);
                RestoreCameras(_state.World);
                _context.Restore(_state.Prefs, _state.World);
                _context.SpaceChanged += OnSpaceChanged;
                if (!RecordId.TryParse(_profile.StartSpace, out var startSpace) || !(_context.Session.World.Records.TryGet(startSpace, out var start) && start is SpaceRecord))
                    throw new DataValidationException(new[] { $"World '{_profile.name}' has no start space '{_profile.StartSpace}'." });
                var firstSpace = KeptSpace(_state.World) ?? startSpace;
                // The first space opens where the camera last was in it; the scene's own view the first time.
                if (_spaceCameras.TryRecall(firstSpace, out var firstPose))
                    PlaceCamera(firstPose);
                _context.ShowSpace(firstSpace);
                var viewMs = Lap(clock);

                _frames = new FrameTimeTracker();
                var diagnostics = new EditorDiagnostics(_view, _streamer, _frames);
                diagnostics.RecordStartup(loadMs, resolveMs, viewMs);
                Debug.Log($"[Forge] Opened '{_profile.name}': {_context.Session.World.Records.Count} records, " +
                          $"load {loadMs:0} ms, resolve {resolveMs:0} ms, first view {viewMs:0} ms ({_view.LoadedChunks.Count} chunks, {_view.PresentedCount} presented).");

                _highlighter = new SelectionHighlighter(_context.Selection, _view, _selectionOverlay, EmphasizedSlot, DropTarget);
                _context.Session.WorldChanged += OnWorldChanged;
                _context.Selection.Changed += _highlighter.Refresh;
                _context.Modes.Changed += OnModeChanged;

                // Not under the world root: the root moves far from zero, and the pictures would jitter with it.
                _thumbnails = new ThumbnailRenderer(assets, null);
                _controller.Initialize(_context, _frame, _view, _waterView, _panel, assets, _agent, _player);
                _panel.Initialize(_context, _controller.Commands, diagnostics, _thumbnails, ground, materials, _controller.Agent?.Conversation, _controller.SnapshotView);
                _panel.Restore(_state.Prefs, _state.World);
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
            // The camera's world position is the same before and after a rebase: the scene moves under it.
            var focus = _frame.ToWorld(_focus.position);
            _frame.Follow(focus);
            // Chunks that stream back in must show their selection tint again.
            if (_streamer.Tick(focus))
                _highlighter.Refresh();
            _waterView.Follow(_focus.position);
            _skyDirector?.Tick(Time.unscaledDeltaTime);
            // Grass grows in a budget of its own: it is near the camera, where a missing patch shows most.
            _grass?.Tick(focus, _frameBudgetMs);
            _grass?.Draw(_focus.position);
            // The selection stays selected while playing, for when editing resumes, but doesn't glow in the game.
            if (!_context.IsPlaying)
                _highlighter.Draw();
            _state.Tick(Time.unscaledDeltaTime);
        }

        // The part a material carried from the assets browser would dress if dropped now.
        (RecordId Id, string Slot)? DropTarget()
        {
            var commands = _controller.Commands;
            if (commands == null || !(_context.Drag is AssetDrag drag) || drag.Kind != AssetDragKind.Material)
                return null;
            return commands.TryPartUnderPointer(out var id, out var slot) ? (id, slot) : ((RecordId, string)?)null;
        }

        // While a select tool is current, the parts a material ball would dress stand out from the rest of the selection.
        string EmphasizedSlot()
        {
            var commands = _controller.Commands;
            return commands != null && ToolCatalog.Of(_context.CurrentTool).WorksOnSelection ? commands.DressedSlot(commands.SelectionSlots()) : null;
        }

        // Everything under the world root moved with it; the camera and the player are not under it and follow by hand.
        void OnSceneShifted(Vector3 delta)
        {
            if (_focus.TryGetComponent<FlyCamera>(out var flyCamera))
                flyCamera.Shift(delta);
            else
                _focus.position += delta;
            _controller.PlayFromHere?.Shift(delta);
            Debug.Log($"[Forge] Scene rebased: {_frame.Origin}.");
        }

        void OnDestroy()
        {
            // Before anything is torn down: the capture reads the context, the camera and the panel.
            if (_streamer != null)
                _state?.Save();

            if (_context != null)
            {
                _context.Session.WorldChanged -= OnWorldChanged;
                _context.SpaceChanged -= OnSpaceChanged;
                _context.Selection.Changed -= _highlighter.Refresh;
                _context.Modes.Changed -= OnModeChanged;
            }

            if (_frame != null)
                _frame.Shifted -= OnSceneShifted;
            _streamer?.Dispose();
            _streamer?.Far?.Dispose();
            _view?.Dispose();
            _waterView?.Dispose();
            _skyView?.Dispose();
            _grass?.Dispose();
            _thumbnails?.Dispose();
        }

        // The camera moves to where it last was in the new space (the scene's own view for the first one), then the
        // space streams in around it at once: a few chunks around the view, the same load as startup.
        void OnSpaceChanged(RecordId space)
        {
            var record = _context.ActiveSpaceRecord;
            if (!_shownSpace.IsDefault)
            {
                _spaceCameras.Remember(_shownSpace, CameraPose());
                PlaceCamera(_spaceCameras.Recall(record));
            }

            _shownSpace = space;
            _interiorFloor.Show(record.Kind == SpaceKind.Interior);
            _waterView.Sync(_context.Session.World, space);
            _streamer.ShowSpace(space, _frame.ToWorld(_focus.position));
            if (_grass != null)
                _grass.Show(_grassSource);
            _highlighter?.Refresh();
        }

        // Playing starts from the editing camera and gives it back exactly where it was, wherever play went meanwhile.
        void OnModeChanged(AppMode mode)
        {
            if (mode == AppMode.Playing)
                _editingPose = CameraPose();
            else if (_editingPose is Pose pose)
            {
                PlaceCamera(pose);
                _editingPose = null;
            }
        }

        // The camera's pose, its position in world coordinates.
        Pose CameraPose() => new Pose(_frame.ToWorld(_focus.position), _focus.rotation);

        // Where the editing camera is: while playing, where it was when play began (what the editor keeps between
        // sessions). None once the camera is destroyed: on shutdown Unity destroys the scene in no set order, so the
        // camera can go before the last save.
        Pose? EditingCameraPose() => _editingPose ?? (_focus != null ? CameraPose() : (Pose?)null);

        // Puts the camera at a pose in world coordinates. Rebased first, so a far-off view lands near the scene's
        // origin, not far from it.
        void PlaceCamera(Pose pose)
        {
            _frame.Follow(pose.position);
            var position = _frame.ToScene(pose.position);
            if (_focus.TryGetComponent<FlyCamera>(out var flyCamera))
                flyCamera.Teleport(position, pose.rotation);
            else
                _focus.SetPositionAndRotation(position, pose.rotation);
        }

        // The space the user left off in, if it is still a space of this world.
        RecordId? KeptSpace(WorldViewState world) =>
            RecordId.TryParse(world.Space, out var space) && _context.Session.World.Records.TryGet(space, out var record) && record is SpaceRecord
                ? space
                : (RecordId?)null;

        void RestoreCameras(WorldViewState world)
        {
            if (world.Cameras == null)
                return;
            foreach (var pair in world.Cameras)
            {
                if (pair.Value != null && pair.Value.IsValid && RecordId.TryParse(pair.Key, out var space))
                    _spaceCameras.Remember(space, new Pose(pair.Value.Position.ToUnity(), pair.Value.Rotation.ToUnity()));
            }
        }

        // What the editor keeps between sessions; the context and the panel write their own parts.
        void CaptureState(EditorPrefs prefs, WorldViewState world)
        {
            // Without a camera, the pose remembered at the last capture (a second ago at most) stands.
            if (EditingCameraPose() is Pose pose)
                _spaceCameras.Remember(_context.ActiveSpace, pose);
            world.Space = _context.ActiveSpace.ToString();
            world.Cameras = world.Cameras ?? new Dictionary<string, SavedPose>();
            foreach (var pair in _spaceCameras.Poses)
                world.Cameras[pair.Key.ToString()] = SavedPose.Of(pair.Value.position.ToNumerics(), pair.Value.rotation.ToNumerics());
            _context.Capture(prefs, world);
            _panel.Capture(prefs, world);
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
            : _selectionOverlay == null ? "EditorApp has no selection overlay material assigned (EditorAppBootstrap._selectionOverlay)."
            : null;

        // The fog is thick where the land stops (just inside the far land's reach, or at the loaded chunks' without far
        // land), so the world fades into the sky instead of ending.
        SkyView CreateSkyView()
        {
            if (_sky == null || _sun == null)
            {
                Debug.LogWarning("[Forge] EditorApp has no sky profile or sun (EditorAppBootstrap._sky, _sun); the scene keeps its own sky.");
                return null;
            }

            var reach = _streamer.Far?.FogReach ?? _loadRadius * WorldGrid.ChunkSize;
            return new SkyView(_sky, _sun, transform, reach);
        }

        // How painted ground looks; optional, so a world without ground textures still opens (unpainted).
        GroundLooks CreateGroundLooks()
        {
            if (_groundLayers != null)
                return new GroundLooks(_groundLayers);

            Debug.LogWarning("[Forge] EditorApp has no ground layer catalog (EditorAppBootstrap._groundLayers); the land is drawn unpainted.");
            return null;
        }

        // How materials look; optional, so a world without the catalog still opens, every object in its own look.
        MaterialLooks CreateMaterialLooks()
        {
            if (_materials != null)
                return new MaterialLooks(new[] { _materials }.Concat(MaterialCatalog.LoadPacks()));

            Debug.LogWarning("[Forge] EditorApp has no material catalog (EditorAppBootstrap._materials); objects keep their own materials.");
            return null;
        }

        // Grass near the camera, grown from the paint; optional, so a world without the material opens bare. Its color is
        // the color its ground shows from afar, so grass and the land under it match.
        GrassView CreateGrass(GroundLooks ground)
        {
            if (_grassMaterial == null)
            {
                Debug.LogWarning("[Forge] EditorApp has no grass material (EditorAppBootstrap._grassMaterial); no grass grows.");
                return null;
            }

            _grassSource = new EditorGrassSource(_context, _view);
            // Shown a space by OnSpaceChanged, like the streamer.
            return new GrassView(transform, _grassMaterial, layer => ground != null ? ground.MapColorOf(layer) : new Color(0.1f, 0.16f, 0.04f));
        }

        // Coarse land beyond the loaded chunks, under the world root like everything else that stands in the world.
        FarTerrainView CreateFarTerrain(GroundLooks ground)
        {
            if (_farTerrainMaterial == null)
            {
                Debug.LogWarning("[Forge] EditorApp has no far terrain material (EditorAppBootstrap._farTerrainMaterial); nothing shows beyond the loaded chunks.");
                return null;
            }

            return new FarTerrainView(transform, _farTerrainMaterial, _farRadius, ground);
        }

        // The sea is as large as the camera can see in every direction, so it reaches the horizon.
        WaterView CreateWaterView()
        {
            if (_water == null)
                Debug.LogWarning("[Forge] EditorApp has no water material (EditorAppBootstrap._water); no water is shown.");

            var reach = _focus.TryGetComponent<Camera>(out var view) ? view.farClipPlane : 1000f;
            return new WaterView(transform, _water, 2f * reach);
        }

        IReadOnlyList<Plugin> LoadMasters(IPluginSerializer serializer, TerrainCache terrain, ObjectCache objects)
        {
            var masters = new List<Plugin>();
            foreach (var folder in _profile.BaseFolders)
            {
                var directory = Path.Combine(Application.streamingAssetsPath, folder);
                // A missing folder would silently open a smaller world, and the edit
                // plugin created for it would lack masters it needs later.
                if (!Directory.Exists(directory))
                    throw new DirectoryNotFoundException($"World '{_profile.name}' needs the plugin folder '{folder}' under StreamingAssets, which does not exist.");
                masters.AddRange(new PluginFileStore(directory, serializer, terrain, objects).LoadAll());
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
