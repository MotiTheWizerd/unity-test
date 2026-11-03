# Forge — Architecture & Conventions

Forge is a prototype of a data-driven world editor (in the spirit of Skyrim's
Creation Kit). It is a test bed for the architecture of a larger open-world game.
The goal is to validate the **data model** (records, plugins and layering, terrain
as data, chunk streaming), not to make things pretty.

**Rule #1: best practice first.** Organized, modular, reusable and DRY. Shortcuts
are allowed only when they are explicitly flagged (see *Shortcuts* below).

---

## Module layout

All project-owned content lives under `Assets/_Project/`. Third-party content
never goes there.

```
Assets/_Project/
  Code/
    Core/        Forge.Core    — engine-free primitives (IDs, results, math, utils)
    Data/        Forge.Data    — engine-free world data model: records, plugins, serialization contracts
    Data.Json/   Forge.Data.Json — JSON plugin format (Newtonsoft), engine-free
    Editing/     Forge.Editing — engine-free edit session (masters + active plugin), commands, world-building commands
    Agent/       Forge.Agent   — engine-free world-building agent: tools over the commands, MCP endpoint, Claude Code backend
    Streaming/   Forge.Streaming — engine-free chunk index and streaming window
    World/       Forge.World   — Unity presentation: terrain chunks, placed objects, streamer
    EditorApp/   Forge.EditorApp — the runtime editor: bootstrap, tools, camera, UI
    UnityEditor/ Forge.UnityEditor — dev tools inside the Unity Editor (Forge menu)
  Tests/
    EditMode/    Forge.Tests.EditMode — unit and architecture tests
  Scenes/        Main.unity (editor scene: EditorApp + Main Camera)
  UI/            EditorPanel.uxml/.uss (runtime editor UI), ForgeRuntimeTheme.tss, ForgePanelSettings
  Content/       PrefabCatalog.asset (asset key → prefab); Placeholder/ holds generated placeholder prefabs + materials
  Shaders/       GizmoOverlay.shader (always-on-top gizmos), PlacementGhost.shader (see-through place preview)
  Materials/     GizmoOverlay.mat, PlacementGhost.mat
  Settings/      URP, input and volume assets; Worlds/ holds the WorldProfile assets; Agent/ the agent's settings and instructions
Assets/StreamingAssets/Plugins/   shipped base plugins (forge.core.json = our "Skyrim.esm")
Assets/StreamingAssets/StressWorld/  generated stress-test world (git-ignored, Forge/Generate Stress World)
<persistentDataPath>/Plugins/     user plugins; the active plugin (forge.user) is saved here
```

Planned modules (each is created when its first code arrives, not before):

| Assembly         | Engine?  | Purpose                                                     |
|------------------|----------|-------------------------------------------------------------|
| `Forge.Game`     | Unity    | The game runtime: player and gameplay over the same data   |

### Dependency rule

Dependencies point **down** only. Cycles are impossible, because asmdefs reject them.

```
          EditorApp                (Game)
       /    |    |    \              |
   Agent  World  |  Data.Json      World
     |      |    |                   |
  Editing   | Streaming          Streaming
      \     |     /
          Data
            |
          Core
```

(EditorApp also references Editing directly; Agent references Data for record ids.)

`Forge.UnityEditor` is editor-only tooling and may reference anything below EditorApp.

- Serialization libraries never enter `Forge.Data`. Each file format is its own
  module (`Forge.Data.Json` today, binary later) implementing `IPluginSerializer`.
- Commands (`Forge.Editing.Commands`) are plain C#: no JSON, no agent protocol. Turning them into tools is
  `Forge.Agent`'s job; `ArchitectureTests` checks Editing references neither Newtonsoft nor Forge.Agent.

- `Forge.Core` and `Forge.Data` set `noEngineReferences: true`. They cannot touch
  `UnityEngine`. This keeps the domain portable, fast to test and independent
  of the engine. Math uses `System.Numerics`. The `World` layer converts to Unity types.
- `ArchitectureTests` re-checks these rules, so loosening an asmdef fails CI.

### Unity naming pitfall

Unity treats any folder named `Editor` as editor-only code that is stripped from
builds. Our **runtime** editor is therefore `EditorApp`, never `Editor`.

---

## Data model (Forge.Data)

**Record ids** are namespaced strings, `namespace:name` (e.g. `forge.core:trees/oak_01`).
They are lowercase ASCII, and the namespace is the id of the plugin that defines the record. Ids
never depend on load order (unlike Skyrim FormIDs), so they don't get renumbered or collide.

**Records** are immutable. Editing creates a new instance that overrides the old
one in the active plugin.
- `BaseObjectRecord`: a definition (display name, asset key, optional category such as
  `Nature/Trees`, normalized by `CategoryPath`; files without a category read as uncategorized),
  and optional `PlacementRules` the editor applies to new instances: random heading, scale range, and
  whether to tilt to the slope. Authoring data only: placed objects store their final transform.
  Absent = placed exactly as aimed, upright. Records hold no derived properties (serializers write
  every public property); helpers like `PlacementRoll.CanVary` live in Editing.
- Optional fields are omitted from plugin JSON rather than written as `null` (`NullValueHandling.Ignore`).
- `PlacedObjectRecord`: an instance in the world (base id, position, rotation, scale).
- New record types must be registered in `RecordTypeRegistry.CreateDefault()`.

**Plugins** = manifest (`id`, `version`, `masters`) + records + deletions.
- A plugin may *define* records only in its own namespace.
- It may *override* or *delete* records only in the namespaces of its masters.

**Load order** (`LoadOrderResolver`):
- Masters must load before their dependents. Duplicates and missing masters are errors.
- v1 rule: **the last plugin to touch a record wins the whole record**.
- **Conflict**: an override whose plugin does not list the previous winner as a
  direct master, so it silently discards another plugin's change. Conflicts are reported, not blocked.
- **Warnings**: overriding or deleting a record that doesn't exist.
- Every validation failure is collected into a single `DataValidationException`.
- The per-record rules live in `ResolutionRules`, and the fold lives in `MasterLayer.Build`
  (`LoadOrderResolver.Resolve` is just that). `MasterLayer.Overlay(top)` layers one more plugin over
  an already-resolved load order without re-reading it, through a `LayeredRecordStore` that
  shares the master store. It costs time in proportion to the top plugin. `MasterLayerTests`
  checks it against full resolution on random top plugins: records, sources, conflicts and warnings.
- `RecordStore.OfType<T>` is cached per type (the store is immutable), so typed scans of
  the masters (e.g. the palette) are paid once.

**File format** (`JsonPluginSerializer`, format version 1):
- Deterministic output (records and deletions sorted) for clean diffs.
- Record types go through the closed registry only. `TypeNameHandling` is
  never used, `$type` metadata is rejected, and unknown fields are errors. Plugin files are untrusted input.

**Terrain**: the world is a grid of `WorldGrid.ChunkSize` (64 m) chunks. A
`TerrainChunkRecord` holds a `HeightField` (2^n+1 samples per edge, meters in
[0, `MaxTerrainHeight`]). Its record name is derived from its coordinate
(`terrain/c{x}_{z}`), so mods reshape terrain by *overriding* the master's chunk.

**Known v1 limits** (deliberate, revisit when needed):
- No field-level merge between conflicting overrides.
- Conflict awareness only checks *direct* masters, not transitive ones.
- Two namespaces defining the same chunk coordinate: the world view shows the
  lowest id and logs a warning.
- Sculpting only edits existing chunks and never creates new ones. Upright objects follow the ground
  by its height change under their pivot; slope objects (fences, rocks) are also re-tilted.
- The edge of the streamed area is a hard cut (no fog, distant LOD or impostors yet).
  Plugins load whole into memory at startup, so only the *scene* streams, not the *data*. A
  30 MB JSON plugin takes ~1.3 s to load.
- Drop to ground samples the terrain under an upright object's pivot only, so on steep slopes part of its base
  can float or clip. Slope objects are seated on their whole footprint, up to a 35° tilt; steeper ground leaves
  their ends off the surface.

---

## Editing and undo (Forge.Editing)

- `EditSession` = read-only masters (resolved once into a `MasterLayer`) + one active plugin,
  overlaid after every edit, undo and redo. For each record id, the active
  plugin is in one of three `EntryState`s: **Absent**, **Written** (add/override) or
  **Deleted** (deletes a master's record). `Put` and `Delete` only set entry states.
- Every edit becomes an `EditStep`: the list of `RecordChange`s (id, before, after).
  **Undo** re-applies the befores in reverse, **redo** re-applies the afters. New tools
  get undo for free as long as they edit through the session.
- `Batch(description, action)` groups several edits into one undo step with a single
  validation. Any failure rolls back the whole batch. Edits that end where they
  started are dropped.
- `IsDirty` compares the current state id with the id saved by `MarkSaved`, so undoing
  back to the saved point clears the dirty flag.
- History is capped (`UndoHistory.DefaultCapacity` = 256). Making a new edit clears redo.
- `Selection` is an ordered set of record ids. It's pruned after every world change, so
  undoing a placement or deleting a record drops it from the selection.
- `ObjectEdits.Transform(ids, change, verb)` is **the one commit path for every transform**:
  one undo step, and objects whose transform doesn't change are skipped. `Move`, `SetPositions`
  (drop to ground) and gizmo drags all go through it. Changing a master's object creates an
  override in the active plugin. `ObjectEdits.Delete` deletes many objects as one step.
- **Gizmo drags** (`Editing/Transforms`) are engine-free `ITransformDrag`s, fed pointer rays:
  `MoveDrag` (axis or plane), `RotateDrag` (around a world axis through the pivot; the angle
  accumulates past ±180°) and `ScaleDrag` (uniform, a ratio of distances along an axis or radially
  on a plane). Preview and commit both call `drag.Apply(original)`, so what is shown is what gets saved.
  Snap steps: 0.5 m, 15°, ×0.1. Several objects turn and scale around the shared pivot.
  Scale is uniform, because `PlacedObjectRecord` has a single scale (as in the Creation Kit).
- `Geometry.DragMath` / `DragConstraint` hold the engine-free ray math: projecting onto an axis,
  intersecting a plane, and the signed angle around an axis.
- `TerrainGrid` (`Editing/Terrain`) reads the world's terrain records as one grid of global samples (chunks
  share their edge samples; lowest id wins on duplicate coordinates; one heightmap resolution): sample heights,
  bilinear heights anywhere, and which chunk holds a sample. Sculpting and the building commands both read it.
- **Terrain edits** (`Editing/Terrain`): a `TerrainEdit` snapshots the terrain and keeps a working copy on
  the **global sample grid**. Subclasses compute every new height from the state before the write, then
  `Write` puts each sample into *every* chunk that shares it. That's why chunk seams can't open.
  `TerrainEdits.Commit` writes the changed chunks as one undo step named by the edit, as overrides of the
  master's chunk records. Two edits exist:
  - `SculptStroke`: brush dabs (`BrushSettings`: Raise / Lower / Flatten / Smooth, radius, strength).
  - `AreaFlattening`: levels a `GroundRect` (center, width across its front, depth, heading, like a placed
    object) to one height and eases back to the natural ground over a blend.
  Both use the same smoothstep edge (`Falloff.Smooth`).
- **Objects follow edited terrain** (`TerrainFollower`; for the sculpt tool the toggle "Objects follow
  terrain", on by default; `flatten_area` always). Each placed object on a touched chunk moves vertically by
  `TerrainEdit.HeightChangeAt` its position, so its clearance above the ground is kept (resting,
  floating or buried). It is data-only, so unloaded objects follow too. Objects are grouped by
  chunk once per stroke. The tool previews them on every dab, and `TerrainEdits.Commit` writes
  terrain and moved objects as the same single undo step.
- Tool pattern for long gestures (move drag, sculpt stroke): **preview on the scene, commit
  one edit on release, reset the preview on cancel or failure**. The session is never touched mid-gesture.
- **Drafts** (`EditSession.BeginDraft` → `EditDraft`): edits made while a draft is open apply and publish the
  world at once, but are collected by the draft (in a `ChangeSet`, first before / latest after per record)
  instead of entering the history. `Accept(description)` records the whole draft as one undo step, `Discard()`
  rolls it back with no history. Undo, redo and `MarkSaved` throw while a draft is open; a failed edit inside
  a draft rolls back only itself. The agent builds into a draft.
- **Placement rules shared by every placer** (`Forge.Editing.Placement`): `PlacementOptions` holds the user's
  settings (random turn and size, align to slope, per-object `RowGap`s) and what they mean per base object;
  `ObjectSeating` is the one seating rule (slope objects on terrain via `GroundFit.TryPose`, everything else
  upright with its lowest point on the ground); `ObjectEdits.Place` creates placed objects as one step with
  ids from `NewIds`. The place tool and the world-building commands all use them, so the agent places exactly
  like the mouse.

## Commands (Forge.Editing.Commands, Forge.Editing.Building)

- An `IEditorCommand` is an editor action defined once: a snake_case `Name`, a `Description` written for
  people and for the agent, and a typed arguments class. `EditorCommand<TArguments, TResult>` checks the
  arguments, then runs; every result is an `ICommandResult` with a one-sentence `Summary`. A refusal is a
  `CommandException` whose message tells the caller (person or agent) what to do instead.
- Each argument property carries a `[Param]` (description, `Optional`, `Minimum`/`Maximum`, `MinItems`/`MaxItems`).
  `CommandParameters` reads them (malformed classes throw at once), `CommandArguments.Validate` enforces them
  before every run, with errors naming the argument as callers write it (`objects[2].scale`), and the agent's
  tool schemas are generated from the same attributes, so what the model is told and what is enforced can't drift.
- `CommandRegistry` holds commands by name. The world-building commands (`BuildingCommands.AddTo`) work on a
  `BuildContext` (session, seating, placement options):
  - `list_objects`: ids, names, categories, footprint (width across the front, depth front to back) and placement rules.
  - `inspect_area`: lowest/highest ground and steepest slope from every terrain sample in the square (exact,
    however coarse a grid the agent asks for), an overview height grid (rows south to north, columns west to
    east), and the objects inside, nearest first. It reads the terrain *data* (`BuildContext.Terrain`, a
    `TerrainGrid` cached per world version), so it answers for unloaded chunks too; placing still seats on the
    loaded scene, and a skipped spot says whether its terrain is missing or just not loaded.
  - `flatten_area`: levels a rectangle (`AreaFlattening`) to a height, by default the average ground height
    inside it (as much dug away as filled in), with a soft edge (default 4 m); objects on the reshaped ground
    follow it (`BuildContext.FollowTerrain`). The agent levels sloped building sites before placing on them.
  - `place_objects`: objects at ground positions, seated on the terrain; heading and scale default to the
    object's natural variation. Unknown ids refuse the whole call; positions without terrain are skipped.
  - `place_line`: one object end to end along a path, stretch by stretch, exactly like the place tool's
    Shift line (`PlacementStroke`); corners shared by two stretches get one object; `closed` encloses an area.
  - `adjust_objects`: moves, turns and resizes placed objects by id; what an item leaves out stays as it is,
    and each object is re-seated on the terrain by `ObjectSeating` at its new spot, then committed through
    `ObjectEdits.Transform` as one step. Unknown ids and spots without terrain are skipped and reported.
  - `delete_objects`: placed objects by id.
- The editor actions that predate the registry (drop to ground, frame, undo/redo, save, the tools' gestures)
  are not commands yet; each moves into the registry when the agent needs it.
- **Headings are compass bearings of the object's front**: 0 north (+z), 90 east (+x). That is the engine's
  yaw, because every asset's front faces local +Z (Unity's forward).

## Streaming (Forge.Streaming + Forge.World)

- `ChunkIndex` (engine-free) groups the presentable records by chunk: a terrain record per
  coordinate (lowest id wins on duplicates) and placed objects by the chunk under their
  position (`WorldGrid.ChunkOf`).
- `ChunkWindow` (engine-free) plans loads and unloads around a focus chunk: load within
  `LoadRadius`, unload beyond `UnloadRadius` (the gap is hysteresis), loads nearest first.
- `WorldView` presents **only loaded chunks**. `LoadChunk` / `UnloadChunk` add or remove a
  chunk's records. Loaded coordinates include empty ones, so content added there later appears.
- `WorldView.Sync(world)` is **incremental** when `ResolvedWorld.TryGetChangesSince` knows the
  changed ids, which it does when both worlds layer over the same `MasterLayer`.
  `ChunkIndex.Update` re-indexes just those ids and returns the dirty chunks, and only loaded
  dirty chunks are re-presented. A change to a non-presentable record (e.g. a base object, which
  many placed objects depend on) falls back to a full rebuild.
- `WorldStreamer` follows the camera. It unloads at once and loads within a per-frame time
  budget (`EditorAppBootstrap`: radius 6 / 8 chunks, 4 ms). A new chunk starts only if the
  average build time still fits. Terrains use `allowAutoConnect`, so LOD edges stitch.
- Measured on the stress world (25,394 records: 1,024 chunks = 2 × 2 km, ~24k objects,
  30 MB JSON), with Unity unfocused, so this is CPU work only and excludes render and destroy cost:
  - startup: load plugins 1,312 ms, resolve 9 ms, first view 211 ms (113 chunks, 2,615 objects)
  - flying at 80 m/s: streaming p50 3.0 ms / p95 4.6 ms / max 9.4 ms per frame, no frame over 16 ms
  - chunk build: ~2.7 ms on average, never more than 12 chunks of backlog
  - one edit (move + undo): **p50 0.04 ms, p95 0.11 ms** with layered resolution and incremental
    index updates. Before that it took 22–55 ms (a full resolve plus a full index rebuild per edit).
- The panel's **Performance** foldout shows these numbers live: chunks, build times,
  worst frame and hitches (>50 ms), last edit and startup phases.

## World-building agent (Forge.Agent)

The user describes what to build ("a fenced house surrounded by trees") and the agent builds it with the
world-building commands. **The model plans; deterministic commands do the geometry** (seating, spacing,
slopes), so results are exact and every edit goes through the edit session.

- **Tools**: `ToolInvoker` turns the command registry into tools: `ToolSchema` builds each JSON schema from
  the `[Param]`s, arguments bind from snake_case JSON (missing or unknown arguments are errors), results go back
  as snake_case JSON. Bad arguments and refusals come back as error results the model reads and corrects.
- **Threading**: the edit session belongs to the main thread. `AgentToolHost` carries every call there through
  a `MainThreadQueue`, which the editor pumps each frame, and takes calls only while a request is open.
- **MCP**: `McpServer` answers MCP's `initialize`, `ping`, `tools/list` and `tools/call` (JSON-RPC 2.0; unknown
  methods such as `server/discover` get "method not found", so clients fall back to `initialize`).
  `McpHttpEndpoint` serves it over Streamable HTTP on 127.0.0.1 at a free port, POST only, behind a fresh
  bearer token compared in constant time; requests with an `Origin` (browsers) are refused. Answers are plain
  JSON; nothing is pushed. Any MCP client could use these tools.
- **Backends** (`IAgentBackend`): `ClaudeCodeBackend` runs each request as `claude -p` under the user's own
  Claude login: `--setting-sources ""` (none of the user's settings files, so their hooks and plugins stay out),
  `--tools ""` (no built-in tools), `--strict-mcp-config` with only the forge endpoint, `--allowedTools mcp__forge`,
  `--permission-mode dontAsk`, `--system-prompt` = the instructions, stream-json output (`ClaudeStream` reads it);
  the message goes in on stdin as UTF-8 (`CommandLine` quotes the rest); follow-ups `--resume` the conversation.
  It runs in `<persistentDataPath>/Agent`. Other providers (an API with its own tool loop) would implement
  `IAgentBackend` over the same `IToolHost`.
- **Conversation** (`AgentConversation`, states Idle / Working / Reviewing): a request opens an `EditDraft`
  and puts the editor briefing in front of the message; the build appears live; then the user **accepts** it
  (one undo step named after the request), **retries** (discard and ask again; the agent is told the first try
  was turned down) or **discards** it, or sends a follow-up that refines the same draft. Stop keeps what was
  built for review. A request that changed nothing needs no review.

## Runtime editor (Forge.EditorApp)

- `EditorAppBootstrap` is the composition root. It opens a **`WorldProfile`** (ScriptableObject in
  `Settings/Worlds`): the base plugin folders under StreamingAssets and the active plugin id.
  `DefaultWorld` = `Plugins` + `forge.user`. `StressWorld` = `Plugins` + `StressWorld` +
  `forge.stress_edits`. Each world has its own edit plugin, because a plugin may only override its
  masters' records. A missing base folder fails loudly instead of opening a smaller world.
  It then wires `EditSession` → `WorldView` + `WorldStreamer` → tools and UI through a shared `EditorContext`.
- **Assets**: records store engine-agnostic asset keys; `IAssetCatalog` turns a key into a scene object.
  `PrefabAssetCatalog` looks the key up in the `PrefabCatalog` ScriptableObject (`Content/PrefabCatalog.asset`,
  assigned on `EditorAppBootstrap._prefabs`) and falls back to `PrimitiveAssetCatalog` (`primitive/*`, and a
  magenta cube for unknown keys). Swapping a prefab in the catalog re-skins every object using that key
  without touching a plugin. Prefab pivots sit at the ground contact point.
- **Placeholder content** (`UnityEditor/Content`): `PlaceholderContent.All` is the one table of the base
  game's placeable objects (record name, display name, category, asset key, how to build the prefab).
  *Forge → Generate Placeholder Content* builds the materials and prefabs from primitives in a preview scene
  and registers them in the catalog; *Forge → Generate Core Plugin* turns the same table into base-object
  records. Regenerating keeps asset GUIDs and each prefab's root id (so references survive) but rewrites the
  ids of the parts inside a prefab, so it shows a diff. `PrefabCatalogTests` fails if a shipped base object
  has no prefab or the catalog lists unused keys.
- `EditorContext.Palette` is an engine-free `ObjectPalette` (`Forge.Editing.Palette`), cached per
  world version. UI and shortcuts read it every frame, and scanning 25k records per frame would cost
  real time. Order: category (uncategorized last), then display name, then id; `Groups` holds the
  same order per category, and 1–9 pick the first nine objects in it. `Filter(query)` keeps objects
  matching every search term in name, category or id.
- `WorldView.Sync(store)` diffs by record reference (records are immutable): unchanged →
  skipped, record changed → **updated in place** (transform / terrain heights), dependency
  (e.g. base object) changed → rebuilt. `RecordLink` maps scene objects back to record ids.
  `PreviewTerrain`/`ResetTerrain` and `ResetTransforms` support tool previews.
- **Tools** (`Tools/IEditorTool`): exactly one is active, chosen through `EditorContext.ActiveTool`.
  `EditorController` handles global shortcuts and passes a filtered `ToolInput` to the
  active tool. Clicks over the UI panel and while the right mouse button is held are removed.
  - `SelectTransformTool`: click / Shift+click to select. Drag the gizmo of the current
    `TransformMode` (W move, E rotate, R scale; Ctrl snaps). Move: arrows (axis) or center ball
    (ground plane). Rotate: X/Y/Z rings. Scale: axis cubes or center cube (camera-facing plane).
    The drag previews through `WorldView.PreviewObject` and commits one `ObjectEdits.Transform`
    on release. Esc cancels and resets the preview. Del deletes the selection.
  - `PlaceTool`: a `PlacementGhost` (a see-through copy built from the same asset catalog, no colliders, no
    shadows, `Shaders/PlacementGhost.shader` via `EditorController._ghostMaterial`) rests on the surface under the
    pointer: its lowest point on the hit (`RendererBounds.PivotAboveBottom`, so center-pivot primitives no
    longer sink halfway), turned by `EditorContext.PlacementYaw` (`[` / `]` = 15°, Shift = 90°, or the panel
    buttons). Surfaces include other objects, so things stack. A click places exactly that pose, which becomes
    the selection. Hidden over the panel. Esc returns to Select.
    With "Random turn & size" on (default), the next placement's `PlacementRoll` (engine-free, `Forge.Editing.Placement`:
    whole degrees, hundredths of scale) is drawn from the base object's variation ahead of the click, so the ghost
    shows it; it is redrawn after each placement and on choosing an object. Trees, bushes and rocks vary; built
    things have no variation and are placed exactly.
    **Painting rows**: holding and dragging runs an engine-free `PlacementStroke` (`Forge.Editing.Placement`). The
    next object is set down once the pointer is clear of the last one: half of each `Footprint` (horizontal bounds
    at scale 1, measured by the ghost) along the drag, plus the gap, at exactly that distance, so gap 0 makes
    fences meet end to end. A stroke only becomes a drag once the pointer is `MinDragDistance` (0.5 m) or half the
    footprint from the start, so hand jitter during a click never turns the object: a click places exactly the
    previewed angle (`[` / `]` included). Once dragging, objects without a random heading take their heading from
    the drag alone (length, local +X, along it; the user's angle is not added, which would set fences across the
    row); the rest keep their drawn heading plus the user's angle. Shift lays a straight row from the press point to the pointer, re-laid live.
    Each position's variation and gap are drawn once, so re-laying doesn't flicker. The first object keeps the
    clicked spot (stacking still works); the others rest on the terrain under them. The row previews as ghosts
    and is placed on release as one undo step ("Place 5 Wooden Fence"), using `EditSession.NewIds` so
    the ids are distinct before any is put; Esc drops the row. Gap and random extra (panel sliders, 0–20 m)
    are remembered per base object for the session: built things start at 0, natural things at 1 m + up to 2 m.
    **Align to slope**: on terrain, objects whose rules ask for it (fences, walls, campfire, bushes, rocks) are tilted by
    the engine-free `GroundFit`: the terrain is sampled at both ends of the footprint along local X and Z (at least
    0.25 m out), the plane through those slopes gives the up direction (capped at 35°), and the pivot sits at their
    average height (the longer axis weighs more, so a fence's ends meet a bump), so a fence's ends both touch a
    hillside. Trees, houses and props stay upright. The panel toggle "Align to slope" (default on) turns it off;
    clicks on top of other objects never tilt.
    **One seating path**: `GroundFit.TryPose` (engine-free: pivot + rotation from a `PlacementShape` = footprint +
    pivot height) is used by placing, by Drop (End) and by sculpting, so all three seat objects identically.
    `SlopeAlignment` (EditorApp) decides what tilts (rules + toggle) and `AssetShapes` measures each asset key once
    (`RendererBounds.TryGetLocal`, mesh bounds in the root's space, works on inactive objects), shared with the ghost.
    Drop keeps an object's heading and re-seats it; `TerrainFollower` re-fits slope objects on old and new ground and
    keeps their clearance (objects on unchanged ground are returned untouched). Upright objects drop and follow
    vertically as before. `ScenePicker.TerrainHeightAt` is the shared terrain probe.
    The ghosts live under one `PlacementGhosts` scene object.
  - **Panel keyboard navigation is off** (`EditorPanel.DisableKeyboardNavigation`): UI Toolkit maps arrows/WASD to
    navigation and Space/Enter to submit, so holding D walked focus from a clicked button into the search field and
    typed into it. Navigation events are swallowed at the root, and holding right mouse keeps UI focus released.
  - `SculptTool` (B): hold left mouse on the terrain. Dabs fire at a fixed 30/s (independent of
    frame rate, capped per frame) and preview live, and the stroke commits on release. `[` / `]`
    change the radius. `BrushCursor` draws a ring that follows the ground, colored by mode.
- **The agent in the editor**: `EditorController` builds an `AgentHost` when the bootstrap has `AgentSettings`
  (`Settings/Agent/AgentSettings.asset`: Claude Code command, model `sonnet`, effort, and the instructions as a
  text asset, `WorldBuilderInstructions.md`). The host registers the building commands over the controller's
  `ObjectSeating` and the context's `PlacementOptions`, pumps the agent's main-thread queue every frame, selects
  the draft's objects (so they stand out and F frames them), and keeps the app running in the background while
  the agent works. `EditorBriefing` tells the agent where the camera looks (the default site), the terrain's
  extent and the selection. **While a draft is open, editing rests**: the controller deactivates the active tool
  (only the camera and F work), the editing dock is disabled, and `EditorContext` refuses edits, undo, redo and
  saving (`IsDraftOpen`). The agent dock on the right (`AgentSection`) shows the transcript, Accept / Retry /
  Discard while reviewing, and a message box (Enter sends, Shift+Enter: new line). The agent's replies are
  Markdown: `Forge.Core.Text.Markdown` (engine-free) parses the subset chat uses into blocks and styled spans, and
  `MarkdownView` lays them out (a label per block, list items as marker + text, rich text wrapped in `<noparse>`
  so the words are never read as tags). Everything else in the transcript is shown exactly as written.
- `EditorCommands` holds one-shot actions shared by shortcuts and UI buttons.
  `DropSelectionToGround` (End) finds the terrain under each selected object, looking through
  other objects, and moves the object so the lowest point of its renderer bounds rests on
  the surface. The whole selection is one undo step. `FrameSelection` (F) frames the selection.
- `ScenePicker` owns all raycasts. Gizmo handles live on the **`ForgeGizmo`** layer
  (TagManager), so world picks and handle picks never interfere.
- `Gizmo` is the shared gizmo machinery: primitive parts tagged with their `GizmoHandle`, hover
  and axis tint through property blocks, and a constant on-screen size. `GizmoShapes` describes the
  translate, rotate (rings of cylinder segments) and scale shapes. Parts use the
  `GizmoOverlay` material (`Shaders/GizmoOverlay.shader`, ZTest Always, unlit), so terrain never hides
  a handle. The material is assigned on `EditorController`. `SelectionHighlighter` tints selected objects (property blocks, so shared
  materials are never touched).
- **UI** (`EditorApp/UI`, UI Toolkit): the layout lives in `UI/EditorPanel.uxml` and the style in
  `UI/EditorPanel.uss`, rendered by a `UIDocument` with `ForgePanelSettings`.
  `EditorPanel` (MonoBehaviour) only composes **one presenter per section**
  (`IPanelSection`: World, Transform, Sculpt, Palette, History, Performance, StatusBar). Each presenter
  binds its UXML elements by name (`PanelElements.Required` fails loudly on a mismatch), sends
  actions to `EditorContext` / `EditorCommands`, and mirrors state in `Refresh()` every frame,
  because shortcuts change the same state. `ChoiceRow<TEnum>` is the reusable segmented control.
  Actions shared by keys and buttons live once on `EditorContext` (`SelectTransformMode`,
  `SelectPlacement`) or `EditorCommands`. The controller and camera only see `IInputBlocker`:
  `IsPointerOverUI` uses `panel.Pick` (the full-screen root ignores picking), and
  `IsKeyboardCaptured` is true while a text input has focus (or the field hosting one: a Slider
  with a number box keeps focus itself), so typing never fires shortcuts. A click in the world or
  Esc releases UI focus; after dragging a brush slider, shortcuts resume on the next world click or Esc.
  `PaletteSection`: a search field (Esc clears) over one foldout per category; folded categories
  stay folded across rebuilds, and a search opens every group with a match. Buttons are plain
  elements, fine for hundreds of objects; a virtualized `ListView` is the next step for thousands.
- Controls: W / E / R → move / rotate / scale (Select tool); 1–9 or the palette → Place tool (`[` / `]` rotate,
  hold + drag paints a row, + Shift a straight one); Esc → Select tool / deselect; Ctrl+Z / Ctrl+Y
  (or Ctrl+Shift+Z) undo/redo; Ctrl+S save; hold right mouse to look, with WASD/QE to fly; wheel
  dollies towards the point under the cursor; hold middle mouse to pan (the grabbed point stays under
  the cursor); F frames the selection.
- `FlyCamera` (implements `ICameraRig`) gets the UI blocker and a depth probe (`ScenePicker.TryPickWorld`)
  from the controller: wheel and pan never start over the panel, and both scale with the depth under the
  cursor. `EditorCommands.FrameSelection` fits the selection's combined renderer bounds through
  `ICameraRig.Frame`. The fit distance is the engine-free `ViewMath.FitDistance` (narrower of the vertical
  and horizontal FOV), and the camera glides there with `SmoothDamp`.
- Runtime-only fields on MonoBehaviours are `[NonSerialized]` (see CLAUDE.md, play-mode reload).

---

## Code conventions

- Namespaces match assemblies: `Forge.Core`, `Forge.Data`, `Forge.World`, …
- One public type per file. The file name matches the type name.
- Domain logic lives in plain C# classes. `MonoBehaviour`s are thin adapters
  (view/controller) with no business logic.
- Data and config are records or `ScriptableObject`s, never hard-coded in scenes.
- Dependencies are explicit (constructor or serialized fields, wired in a
  composition root). No singletons, no `FindObjectOfType`, no static mutable state.
- Systems decouple through events or messages, not direct cross-module calls.
- Composition over inheritance. Content reuse goes through prefabs and prefab variants.
- Assets face local +Z (Unity's forward): a house's door, a bench's seat. Pivots sit at the ground contact point.
- A new editor action is a command (`Forge.Editing.Commands`): defined once, with its description and `[Param]`s,
  so the UI, shortcuts and the agent all reach it. A feature is done when the agent can use it too.
- `internal` by default. Tests access internals via `InternalsVisibleTo`.
- Style is enforced by `.editorconfig`.

## Testing

- Unity Test Framework, `com.unity.test-framework`.
- EditMode tests cover all engine-free logic. PlayMode tests only when the engine is
  truly involved.
- Every new behaviour in `Core` or `Data` ships with tests.

## Source control

- Git. `.gitignore` excludes Unity-generated folders (`Library/`, `Temp/`, `Logs/`,
  `obj/`, `UserSettings/`, `Build*/`).
- Assets are serialized as **text** (Force Text) with visible `.meta` files. Every
  asset is committed together with its `.meta`, which holds the GUID that all
  references use.
- Binary art (textures, models, audio) goes through Git LFS (see `.gitattributes`).
- Move and rename assets **inside Unity** (or via MCP), never in the file system,
  so GUIDs and references are preserved.

## Shortcuts

Every deliberate shortcut is marked in code with `// SHORTCUT:` plus the reason
and the condition for removing it, and is listed here.

| Where | What | Why | Remove when |
|-------|------|-----|-------------|
| `Packages/manifest.json` | MCP for Unity tracks the git default branch, not a release tag | Fast setup. The exact commit is locked in `packages-lock.json` | Pin to a release tag on the next package update |
| `Data.Json/Converters.cs` `HeightFieldConverter` | Terrain heights embedded in plugin JSON as base64 | Keeps a plugin a single file for the walking skeleton | Plugins become packages (folder/zip) with binary side files for heightmaps and assets |
| `World/Assets/PrefabCatalog.cs` | Catalog entries reference prefabs directly, so all content loads with the catalog at startup | A dozen placeholder prefabs fit in memory easily | Content outgrows memory → load prefabs by asset key through Addressables, streamed with the chunks |
| `EditorApp/EditorController.cs`, `UI/TransformSection.cs` | Input read by polling devices, not an Input Actions asset; the panel repeats the W/E/R labels | Skeleton has few fixed bindings | Rebindable controls are needed → Input Actions asset, labels read from it |
