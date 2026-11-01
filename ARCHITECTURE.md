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
    World/       Forge.World   — Unity presentation: terrain chunks, placed objects
    EditorApp/   Forge.EditorApp — the runtime editor: bootstrap, tools, camera, UI
    UnityEditor/ Forge.UnityEditor — dev tools inside the Unity Editor (Forge menu)
  Tests/
    EditMode/    Forge.Tests.EditMode — unit and architecture tests
  Scenes/        Main.unity (editor scene: EditorApp + Main Camera)
  Settings/      URP, input and volume assets
Assets/StreamingAssets/Plugins/   shipped base plugins (forge.core.json = our "Skyrim.esm")
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
       \    |    /
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
- `BaseObjectRecord`: a definition (display name, asset key).
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
- Sculpting doesn't move placed objects, so they can float or sink after terrain edits
  (no "drop to ground" yet). Sculpting only edits existing chunks and never creates new ones.

---

## Editing and undo (Forge.Editing)

- `EditSession` = read-only masters + one active plugin. For each record id, the active
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
- `ObjectEdits.Move/Delete` act on any number of placed objects as one undo step.
  Moving a master's object creates an override in the active plugin.
- `Geometry.DragMath` holds the engine-free ray math for translate handles
  (axis projection, plane intersection).
- **Terrain sculpting** (`Editing/Terrain`): a `SculptStroke` snapshots the terrain and
  keeps a working copy. Dabs (`BrushSettings`: Raise / Lower / Flatten / Smooth, radius,
  strength, smoothstep falloff) run on the **global sample grid**: pass 1 computes every
  new height from the pre-dab state, pass 2 writes each sample into *every* chunk that
  shares it. That's why chunk seams can't open. `TerrainEdits.Commit` writes the changed
  chunks as one undo step, as overrides of the master's chunk records.
- Tool pattern for long gestures (move drag, sculpt stroke): **preview on the scene, commit
  one edit on release, reset the preview on cancel or failure**. The session is never touched mid-gesture.

## Runtime editor (Forge.EditorApp)

- `EditorAppBootstrap` is the composition root. It loads base plugins from StreamingAssets and the
  active plugin from the user folder (or creates it with all base plugins as masters),
  then wires `EditSession` → `WorldView` → tools and UI through a shared `EditorContext`.
- `WorldView.Sync(store)` diffs by record reference (records are immutable): unchanged →
  skipped, record changed → **updated in place** (transform / terrain heights), dependency
  (e.g. base object) changed → rebuilt. `RecordLink` maps scene objects back to record ids.
  `PreviewTerrain`/`ResetTerrain` and `ResetTransforms` support tool previews.
- **Tools** (`Tools/IEditorTool`): exactly one is active, chosen through `EditorContext.ActiveTool`.
  `EditorController` handles global shortcuts and passes a filtered `ToolInput` to the
  active tool. Clicks over the UI panel and while the right mouse button is held are removed.
  - `SelectMoveTool`: click / Shift+click to select. Drag a gizmo arrow (axis) or the center ball
    (horizontal plane). The drag previews on the scene objects and commits one
    `ObjectEdits.Move` on release; Esc cancels and resets the preview. Del deletes the selection.
  - `PlaceTool`: click a surface to place the palette object, which becomes the selection. Esc returns to Select.
  - `SculptTool` (B): hold left mouse on the terrain. Dabs fire at a fixed 30/s (independent of
    frame rate, capped per frame) and preview live, and the stroke commits on release. `[` / `]`
    change the radius. `BrushCursor` draws a ring that follows the ground, colored by mode.
- `ScenePicker` owns all raycasts. Gizmo handles live on the **`ForgeGizmo`** layer
  (TagManager), so world picks and handle picks never interfere.
- `TranslateGizmo` is built from primitives, tinted with property blocks and sized to the
  camera distance. `SelectionHighlighter` tints selected objects (property blocks, so shared
  materials are never touched).
- Controls: 1–9 or the palette → Place tool; Esc → Select tool / deselect; Ctrl+Z / Ctrl+Y
  (or Ctrl+Shift+Z) undo/redo; Ctrl+S save; hold right mouse to look, with WASD/QE to fly.
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
| `Editing/EditSession.cs` `Resolve` | Whole load order re-resolved on every edit | Simple and obviously correct at prototype scale | Edits feel slow on large worlds → incremental resolution |
| `EditorApp/EditorOverlay.cs` | Immediate-mode GUI (OnGUI) panel | Fastest way to a usable skeleton | Editor UI design (Phase 4) → UI Toolkit |
| `EditorApp/Gizmos/TranslateGizmo.cs` | Gizmo uses the default lit material, so terrain can hide it | No custom always-on-top shader yet | Gizmo occlusion bothers users → overlay shader or a separate camera layer |
| `EditorApp/EditorController.cs` | Input read by polling devices, not an Input Actions asset | Skeleton has few fixed bindings | Rebindable controls are needed → Input Actions asset |
