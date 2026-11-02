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
    Editing/     Forge.Editing — engine-free edit session (masters + active plugin)
    Streaming/   Forge.Streaming — engine-free chunk index and streaming window
    World/       Forge.World   — Unity presentation: terrain chunks, placed objects, streamer
    EditorApp/   Forge.EditorApp — the runtime editor: bootstrap, tools, camera, UI
    UnityEditor/ Forge.UnityEditor — dev tools inside the Unity Editor (Forge menu)
  Tests/
    EditMode/    Forge.Tests.EditMode — unit and architecture tests
  Scenes/        Main.unity (editor scene: EditorApp + Main Camera)
  UI/            EditorPanel.uxml/.uss (runtime editor UI), ForgeRuntimeTheme.tss, ForgePanelSettings
  Content/       PrefabCatalog.asset (asset key → prefab); Placeholder/ holds generated placeholder prefabs + materials
  Shaders/       GizmoOverlay.shader (always-on-top gizmos)
  Materials/     GizmoOverlay.mat
  Settings/      URP, input and volume assets; Worlds/ holds the WorldProfile assets
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
        EditorApp            (Game)
       /    |    \             |
 Editing  World  Data.Json   World
    |       |                  |
    |   Streaming          Streaming
     \      |      /
          Data
            |
          Core
```

`Forge.UnityEditor` is editor-only tooling and may reference anything below EditorApp.

- Serialization libraries never enter `Forge.Data`. Each file format is its own
  module (`Forge.Data.Json` today, binary later) implementing `IPluginSerializer`.

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
  `Nature/Trees`, normalized by `CategoryPath`; files without a category read as uncategorized).
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
- Sculpting only edits existing chunks and never creates new ones. Objects follow the ground
  by its height change under their pivot only (no tilting to the new slope).
- The edge of the streamed area is a hard cut (no fog, distant LOD or impostors yet).
  Plugins load whole into memory at startup, so only the *scene* streams, not the *data*. A
  30 MB JSON plugin takes ~1.3 s to load.
- Drop to ground samples the terrain under the object's pivot only. On steep slopes, part of
  the base can float or clip, and objects aren't tilted to match the slope.

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
- **Terrain sculpting** (`Editing/Terrain`): a `SculptStroke` snapshots the terrain and
  keeps a working copy. Dabs (`BrushSettings`: Raise / Lower / Flatten / Smooth, radius,
  strength, smoothstep falloff) run on the **global sample grid**: pass 1 computes every
  new height from the pre-dab state, pass 2 writes each sample into *every* chunk that
  shares it. That's why chunk seams can't open. `TerrainEdits.Commit` writes the changed
  chunks as one undo step, as overrides of the master's chunk records.
- **Objects follow sculpted terrain** (`TerrainFollower`, toggle "Objects follow terrain", on by
  default). Each placed object on a touched chunk moves vertically by
  `SculptStroke.HeightChangeAt` its position, so its clearance above the ground is kept (resting,
  floating or buried). It is data-only, so unloaded objects follow too. Objects are grouped by
  chunk once per stroke. The tool previews them on every dab, and `TerrainEdits.Commit` writes
  terrain and moved objects as the same single undo step.
- Tool pattern for long gestures (move drag, sculpt stroke): **preview on the scene, commit
  one edit on release, reset the preview on cancel or failure**. The session is never touched mid-gesture.

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
  - `PlaceTool`: click a surface to place the palette object, which becomes the selection. Esc returns to Select.
  - `SculptTool` (B): hold left mouse on the terrain. Dabs fire at a fixed 30/s (independent of
    frame rate, capped per frame) and preview live, and the stroke commits on release. `[` / `]`
    change the radius. `BrushCursor` draws a ring that follows the ground, colored by mode.
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
- Controls: W / E / R → move / rotate / scale (Select tool); 1–9 or the palette → Place tool; Esc → Select tool / deselect; Ctrl+Z / Ctrl+Y
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
