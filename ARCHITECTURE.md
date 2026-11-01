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
  Tests/
    EditMode/    Forge.Tests.EditMode — unit and architecture tests
  Scenes/        Main.unity (bootstrap scene)
  Settings/      URP, input and volume assets
```

Planned modules (each is created when its first code arrives, not before):

| Assembly         | Engine?  | Purpose                                                     |
|------------------|----------|-------------------------------------------------------------|
| `Forge.World`    | Unity    | Presents data in Unity: terrain chunks, placed objects, streaming |
| `Forge.EditorApp`| Unity    | The runtime editor application: tools, UI, commands, undo  |
| `Forge.Game`     | Unity    | The game runtime: player and gameplay over the same data   |
| `Forge.UnityEditor` | Editor only | Dev-time tooling inside the Unity Editor (if ever needed) |

### Dependency rule

Dependencies point **down** only. Cycles are impossible, because asmdefs reject them.

```
EditorApp   Game
      \     /
       World ──── Data.Json
         |       /
       Data ────
         |
       Core
```

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

**Known v1 limits** (deliberate, revisit when needed):
- No field-level merge between conflicting overrides.
- Conflict awareness only checks *direct* masters, not transitive ones.
- Terrain records are not designed yet (Phase 3).

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
