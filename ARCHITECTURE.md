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
    Streaming/   Forge.Streaming — engine-free chunk index, streaming window, scene origin, far tiles
    Game/        Forge.Game    — engine-free game rules: the player's look and movement over a motor, attributes,
                               the game's event stream, actors' ids, motion (animation input), HUD timing,
                               interaction (the use key), items and inventories, what playing changes in the world
    World/       Forge.World   — Unity presentation: terrain chunks, far land, placed objects, streamer
    Play/        Forge.Play    — Unity side of playing: the player session, its body (CharacterController), model,
                                 controls, camera director and profile
    EditorApp/   Forge.EditorApp — the runtime editor: bootstrap, tools, camera, UI
    UnityEditor/ Forge.UnityEditor — dev tools inside the Unity Editor (Forge menu)
  Tests/
    EditMode/    Forge.Tests.EditMode — unit and architecture tests
  Scenes/        Main.unity (editor scene: EditorApp + Main Camera)
  UI/            EditorPanel.uxml/.uss (runtime editor UI), ForgeRuntimeTheme.tss, ForgePanelSettings
  Content/       PrefabCatalog.asset (asset key → prefab), GroundLayerCatalog.asset (asset key → terrain layer);
                 Placeholder/ holds generated placeholder prefabs + materials; Ground/ the ground textures (CC0);
                 Characters/ one folder per rigged character (model, texture, built material and prefab);
                 Models/ one folder per static model (the real art: model, textures, built material and prefab)
  Shaders/       GizmoOverlay.shader (always-on-top gizmos), PlacementGhost.shader (see-through place preview), Water.shader (sea and lakes), Sky.shader (the skybox)
  Materials/     GizmoOverlay.mat, PlacementGhost.mat, Water.mat, Sky.mat, InteriorFloor.mat (runtime code copies
                 materials assets reference, never Shader.Find, so builds include the shaders)
  Settings/      URP, input (InputSystem_Actions: the project-wide actions, its Player map drives the player) and volume assets;
                 Worlds/ holds the WorldProfile assets; Agent/ the agent's settings and instructions; Sky/ the SkyProfile assets;
                 Player/ the PlayerProfile
Assets/StreamingAssets/Plugins/   shipped base plugins (forge.core.json = our "Skyrim.esm", its heights in forge.core.<hash>.terrain)
Assets/StreamingAssets/StressWorld/  generated stress-test world (git-ignored, Forge/Generate Stress World)
<persistentDataPath>/Plugins/     user plugins; the active plugin (forge.user) is saved here
<persistentDataPath>/Editor/      the editor's state between sessions: prefs.json (the user's), Worlds/<edit plugin id>.json (per world)
Builds/Windows/Forge.exe          the standalone player (git-ignored; Forge → Build and Run (Windows), `PlayerBuilder`):
                                  fullscreen at the screen's resolution, same base and user plugins as Play mode
```

Planned modules (each is created when its first code arrives, not before):

| Assembly         | Engine?  | Purpose                                                     |
|------------------|----------|-------------------------------------------------------------|
| `Forge.GameApp`  | Unity    | The shipped game's composition root: the world played, no editor. `TerrainGrid` and `WaterMap` move below `Forge.Editing` for the game (it asks them for ground and swimming) |

### Dependency rule

Dependencies point **down** only. Cycles are impossible, because asmdefs reject them.

```
              EditorApp
       /    |    |    \     \
   Agent  World  |  Data.Json  Play ── World
     |      |    |              |
  Editing   | Streaming        Game
      \     |     /             |
          Data ─────────────────┘
            |
          Core
```

(EditorApp also references Editing directly; Agent references Data for record ids. `Forge.Game` needs Core and
Data, whose records its rules read (attributes); it never sees the editor, and `Forge.Play` is the one place the
rules meet the engine.)

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
- **Parts**: what a base object is beyond its look comes in optional parts, not in other record types, so every kind of
  thing is placed, dressed, varied and pictured the same way, and a mod gives an existing object a part by overriding it.
  The first is **`ItemPart`** (`BaseObjectRecord.Item`; in JSON `"item": {"kind": "potion", "weight": 0.5, "value": 15}`,
  omitted when it is no item): something an actor picks up and carries. `ItemKind` (misc, weapon, potion, food; by name,
  misc when left out) is where an inventory lists it, grouping only; weight (≥ 0) counts against carry weight; value
  (≥ 0) is in gold. What using an item does (wielding, wearing, drinking) will be parts of their own; doors and
  containers will be parts too. A variant of an item is an item (`MakeVariant` keeps the parts).
- Optional fields are omitted from plugin JSON rather than written as `null` (`NullValueHandling.Ignore`).
- `PlacedObjectRecord`: an instance in the world (base id, position, rotation, scale), and optionally the
  `Materials` it wears instead of its base object's in some **material slots** (`MaterialOverride`: slot, material id;
  sorted by slot, each slot once, at most 64; empty = the base object's look). A slot is named after the material the
  asset wears there by default ("Roof", "Plaster"), as Blender and Unreal name a mesh's slots, so the data never
  depends on how an asset is put together inside; every part wearing that material is one slot. A base object may
  carry overrides too (`BaseObjectRecord.Materials`, null when none; in JSON `"materials": {"Roof": "…:material/thatch"}`,
  `MaterialOverridesConverter`): a **variant**, the Creation Kit's way to vary an object. What an instance wears is
  `MaterialOverrides.Layer(base, own)`, its own winning slot by slot (`MaterialOverrides` holds the list rules both
  share). `ObjectEdits.MakeVariant(placed, name)` saves a placed object's look as a new base object in the active plugin
  (`variant/<n>`: same asset, category and placement rules, the layered materials) and makes the object an instance of
  it, one undo step; the Materials page's **Make variant** (one object selected) asks for the name, suggested from what
  it wears ("Small House (Cut Stone, Thatch)"), and the new object becomes the palette's pick. Its tile's picture wears
  its materials (`IAssetThumbnails.Get(assetKey, lookKey, dress)`, `MaterialLooks.Dress` / `KeyOf`).
- `MaterialRecord` (`material/<name>`): a surface objects can wear (display name, asset key the presentation resolves
  to the look), like a ground layer: a mod restyles a material everywhere by overriding it, or adds its own.
  `forge.core` ships eight (`CoreMaterials`). More come as **material packs** (Content/MaterialPacks/<Pack>): a
  pack.json (plugin id; per material its folder, name, category and ambientCG source) and a Materials folder of
  texture folders. `Tools/fetch_material_pack.py <pack>` downloads the sources; Forge → Materials → Build Material
  Packs (`MaterialPackBuilder`, sharing `MaterialBuilder.BuildFolders`) builds each folder's material into the pack's
  own catalog under Resources/MaterialPacks (keys "material/<pack>/<folder>", so packs never clash) and writes the
  pack's plugin of material records to StreamingAssets/Plugins. The editor layers the base catalog with every pack
  catalog found there (`MaterialCatalog.LoadPacks`, `MaterialLooks` takes a list), so a pack needs no reference
  anywhere. The local TestPack (26 materials, plugin forge.testpack) is git-ignored: whether to commit or curate it
  is still open.
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
  the masters (e.g. the palette) are paid once. Placed objects are the exception: they are resolved a block at a
  time (`MasterObjects`), when `IRecordStore.ObjectsIn(space, chunk)` first asks for a chunk, and kept; the fold
  per block is the same last-toucher-wins rule, with an id a later plugin writes left to that plugin's block
  (which may be another chunk) and one a later plugin deletes left out. The resolved count, the warnings and the
  conflicts come from each plugin's `Overrides` and deletions through `TryLocate`, so resolving reads no block.
  `TryGet` of an object finds its block through the last writer's locator. `ObjectChunks(space)` lists the chunks
  with objects. `All` and `OfType<PlacedObjectRecord>` read every block; the chunk-scoped members are the hot path
  (`ChunkIndex`, `TerrainFollower`, `BuildContext.PlacedObjects`). `LayeredRecordStore` merges the top plugin's
  objects into each block it is asked for.

**File format** (`JsonPluginSerializer`, format version 3):
- Deterministic output (records and deletions sorted) for clean diffs.
- Record types go through the closed registry only. `TypeNameHandling` is
  never used, `$type` metadata is rejected, and unknown fields are errors. Plugin files are untrusted input.
- Read in one streaming pass over the fields in written order: `format` first (formats 1 and 2, which kept heights
  and then objects in the JSON, are refused by name), then `manifest`, `terrain`, `objects`, `records`, `deletions`.
- **Terrain heights are not in the JSON.** A plugin with terrain names its terrain file (`"terrain"`), and each
  chunk's `heights` is only `{"resolution", "block"}`, a block of that file. See *Terrain storage*.
- **Placed objects are not in the JSON at all.** A plugin with objects names its objects file (`"objects"`); a
  placed object among the `records` is an error. See *Objects storage*. Regenerate `forge.core` and the stress world
  after a format change (`Forge/Generate Core Plugin`, `Forge/Generate Stress World`); older user plugins don't load.

**Placed objects by chunk** (`IObjectBlocks`, `ObjectBlockKey` = space + chunk): a plugin keeps its placed objects
apart from its records, a block per chunk, so a loaded plugin's objects stay on disk until their chunk is needed.
`Plugin.Records` never holds them; `Plugin.Objects` does: `InMemoryObjectBlocks` for a plugin being edited, built or
tested (the in-memory constructor sorts placed objects out of the records it is given), an `ObjectsFile` for a loaded
one. What any block source knows without reading a block: its keys and counts, which block an id is in
(`TryLocate`), and its `Overrides` (object ids in other plugins' namespaces). The editor's active plugin is held
whole, objects included; it is the one plugin being written.

**Spaces** (`SpaceRecord`, `space/<name>`), like Bethesda worldspaces and interior cells: the world is made of
separate spaces, each with its own coordinates. A *worldspace* is open land; an *interior* (a house, a cave) has
no terrain, and the editor gives it a floor grid at height 0 to stand on. Every `TerrainChunkRecord` and
`PlacedObjectRecord` names its `Space`. A worldspace has no size field: its extent is its chunks, so land is
added as chunks, and a mod adds land to its master's worldspace with chunks in its own namespace. A new
worldspace starts as flat land of 3 to 64 chunks per edge (`SpaceEdits.Sizes`, `MaxSize`; 64 ≈ 4 km), and grows
afterwards with `SpaceEdits.ExtendLand`: a strip of chunks along one side (`LandSide`), the full reach of the land
(`LandBounds`), at the base height, each new chunk ramping from the existing edge so the join is seamless; one undo
step. Several sides extend in one step too (`LandSides.All`): north, south, east, west in turn, each strip along the
land as grown so far, so the corners fill in, ramping from the strips beside them. The SPACE section shows the
reach with N / S / E / W / All buttons, and the agent has `extend_land` (side `all` for every side). The only
cap is a sanity bound of 320 chunks per axis (`MaxExtent`, ≈ 20 km). Far from the world's origin the scene is
rebased under the camera (*origin shifting*, see Streaming), so 32-bit float jitter never shows, and the land past
the loaded chunks is drawn as coarse *far tiles* out to a kilometre (see Streaming). Placed objects already
load a chunk at a time (*Objects storage*), and the stress world's 24k objects cost 94 ms and 2 MB to open. The
editor shows and edits one space at a time (`EditorContext.ActiveSpace`); the chunk index, terrain grid, terrain edits,
terrain followers and the agent's commands are all per space.
A worldspace may have a **sea**: `SpaceRecord.WaterLevel` (meters, optional, omitted from the JSON when there is
none; interiors never have one). All ground below it is under water, coasts and hollows alike; beyond the land the sea
goes on. It is the first of three water layers: the **data** (records, saved in plugins) and the **query**
(`WaterMap`) are fixed contracts, the **look** (`SeaSurface`, `LakeSurfaces` + `Water.shader`) is meant to be replaced.
**Lakes** (`LakeRecord`, `lake/<n>`): a point in the water (x, z) and a surface `Level`. Their shape is not stored:
the water fills every terrain sample below the level connected to the point (`LakeFill`), so the shore follows
sculpting and a mod that reshapes the land reshapes its lakes. Rivers (splines) come later as their own records and
join the same `WaterMap`.

**Time and the sun** (`Forge.Data.Sky`, engine-free, so the game will use the same): `WorldClock` holds the day number
and the hour, running at `Scale` game seconds per real second (20, as in Skyrim) while `Running`; in the editor it is a
view setting (`EditorContext.Clock`, the TIME OF DAY slider, Run toggle and Day field, kept with the editor's state, never
in a plugin), the game will save it.
`DayCycle` is the shape of a day (sunrise, sunset, noon height; `DayCycle.Default` = 6:00, 20:00, 60°, every day of
a worldspace without a climate; `DayCycle.Between` blends two). `SunPath.At(day, hour)` is a stylized arc (`SunPath.Arc`): rising east,
highest south midway, setting west, then under the horizon through the north, `NightDepth` (40°) down at the middle of
the night, continuous at sunrise and sunset; it returns a `SkyPosition` (height, bearing), as the moons do.
**Moons**: a `Moon` is a name, a cycle in days (`Period`), its phase on day 0, its size and the top of its arc
(`Moon.Defaults`: a Greater Moon, 24 days, nearly full on day 0, and a Lesser Moon, 10 days, for a worldspace without
a climate; at most `Moon.MaxPerSky`, 4, in one sky). `MoonPath.At(moon, cycle, day, hour)` gives a `MoonState`: the phase from the clock's day
and hour, the lit share, and a position on the sun's arc at the moon's own height, running behind the sun by
phase × 24 hours, so a new moon travels with the sun, a full moon rises at sunset and stands highest at night, and a
moon rises later each day. `SkyState.At(cycle, moons, day, hour)` is the whole sky at a moment: the day's shape, the
sun, every moon and the stars' turn (once around the pole star a day).
**Climates** (`ClimateRecord`, `climate/<name>`, like Bethesda's): a year length in days, a midsummer and a midwinter
`DayCycle`, and the moons. A worldspace names one in `SpaceRecord.Climate` (optional, omitted from the JSON when
there is none; interiors never have one), so one climate serves many worldspaces and a mod restyles them all by
overriding it. `Seasons` is the year, the astronomers' way: day 0 is the first day of spring, four equal seasons
(`SeasonTime`: year, day of the year, season, early/mid/late third), summer begins on the longest day and winter on the
shortest, and `ClimateRecord.DayOn(day)` blends the two days by a sine over the year (½ at the equinoxes). Without a
climate (or with one no loaded plugin has) a worldspace has `DayCycle.Default` and `Moon.Defaults` every day.
`forge.core` ships Temperate (the main world's), Northern (20-hour summer days, 5-hour winter ones) and Desert
(`CoreClimates`), all 120-day years. Weather will join the climate record. `EditorContext.ActiveClimate` and
`EditorContext.Sky` give the space on show's climate and sky; the SPACE section's Climate picker sets it (one undo
step, `SpaceEdits.SetClimate`) and the agent's `set_climate` does too. The agent's briefing names the time, the day,
the season and climate, today's sunrise and sunset, whether the sun is up, and each moon's place (up or down) and
phase.

**Terrain**: a worldspace is a grid of `WorldGrid.ChunkSize` (64 m) chunks. A
`TerrainChunkRecord` holds `ChunkHeights` (2^n+1 samples per edge, meters in
[0, `MaxTerrainHeight`]; new land starts at `WorldGrid.DefaultLandHeight`, 50 m). **Sample spacing is chosen per
worldspace** when it is created: `SpaceRecord.HeightmapResolution` (65 = a sample every meter, 33 = every 2 m,
`SampleSpacing` says which; `WorldGrid.SpacingOf` / `ResolutionFor` convert) is the resolution every chunk of
that space is made at, by `CreateWorldspace` and `ExtendLand`, and `TerrainGrid` insists on one resolution per
space. 2 m is a quarter of the data for open country; 1 m stays for detailed areas, as their own worldspace. Its record name is derived
from its space and coordinate (`terrain/{space ns}/{space name}/c{x}_{z}`), so mods reshape terrain by
*overriding* the master's chunk.

**Ground paint**, like a Bethesda LAND record's texture layers: what the land looks like, on the same chunk record as
its heights, so one override reshapes and repaints. **Ground layers** (`GroundLayerRecord`, `ground/<name>`) are the
kinds of ground: a display name and an asset key the presentation resolves to the look (textures), so a mod restyles a
layer everywhere by overriding it, or adds its own. `forge.core` ships grass, dirt, rock and sand (`CoreGroundLayers`).
A chunk's `TerrainChunkRecord.Paint` is a `ChunkPaint` (null: unpainted, shown as the base ground): the layers it
names (1 to `ChunkPaint.MaxLayers`, 8) and, per texel (`WorldGrid.PaintResolution`, 64 per edge: one per square
meter, covering the chunk edge to edge, none shared with a neighbour), a byte per layer, the bytes of a texel adding
up to 255 (`GroundPaint.Full`). In memory it is a `GroundPaint`; painting builds one from amounts
(`GroundPaint.FromWeights`: scaled per texel and rounded so the bytes add up exactly, layers that show nowhere left
out, the faintest let go past eight). In the JSON a chunk's `paint` is `{"layers": [...], "block"}` (optional, omitted
when unpainted, so terrain from before paint reads unchanged, format 3 as before); the weights are a block of the
terrain file.

**Terrain storage** (`Forge.Data.Serialization`, engine-free): a record's `ChunkHeights` are either in memory (a
`HeightField`: new or edited land) or stored in its plugin's terrain file and read on first `Load()`; `Resolution` is
known without loading. Every reader loads once per use, not per sample (`TerrainGrid.HeightsOf` holds a chunk's heights
for the grid's life; the world view loads a chunk when it is shown). So a world opens by reading its JSON and the
terrain files' block tables only, and only land in use is in memory.
- **The file** (`TerrainFile`, `{plugin}.{16 hex of SHA-256}.terrain` next to the plugin): a 24-byte header ("FTRN",
  version, meters per step, block count, table offset), the blocks, then the block table (offset, length,
  resolution, codec). Opening reads the header and table; the file is not held open. The codec tells a heights block
  (`HeightCodec`, 1) from a paint block (`PaintCodec`, 2: the layer count, then each layer's plane as differences
  from the byte to the left, Deflated; a one-layer chunk is a few dozen bytes). Heights blocks come first, then paint,
  so terrain without paint writes the file it always did. Paint is read lazily like heights (`StoredPaint`).
- **The codec** (`HeightCodec`): heights as 16-bit steps of 1/256 m (under 4 mm; round heights such as 50 m stay exact;
  65,535 steps reach past 200 m), each stored as its difference from left + above − above-left, zigzagged, split into
  low- and high-byte planes, Deflated. Flat land is ~40 bytes a chunk, rolling hills ~3 KB (vs ~22 KB of base64 floats
  before); decoding takes ~0.2 ms a chunk.
- **The cache** (`TerrainCache`): decoded heights and paint of stored chunks up to one byte budget (64 MB default,
  ~2,000 chunks with two layers of paint), least recently used let go first. One cache is shared by all of a world's
  stores (`EditorAppBootstrap`).
- **Saving** (`PluginFileStore.Save`): the terrain file is written first (`TerrainFileWriter`: one block per distinct
  heights or paint instance, so a new worldspace's flat chunks share one; stored blocks are copied packed, without decoding),
  flushed, and named by its content's hash (same content, same file). Then the JSON, which names it, replaces the old
  one atomically: the commit point, so a crash at any step leaves a matching pair. Then the plugin's other terrain files
  go, except the one it was loaded with, whose blocks the open world still reads. Measured on 1,024 rolling-hill chunks:
  3 MB terrain + 267 KB JSON, saved in 0.2 s, re-saved unchanged in 0.1 s, opened in 24 ms, every chunk decoded in 0.17 s.
- Untrusted input: a plugin may only name a `.terrain` file of its own next to it (no paths); damaged files and blocks
  are `DataValidationException`s naming the file.

**Objects storage** (`Forge.Data.Serialization`, engine-free): a loaded plugin's placed objects are an `ObjectsFile`
(`{plugin}.{16 hex of SHA-256}.objects` next to the plugin), a block per chunk, read when `IObjectBlocks.Read` first
asks for the block and kept in the shared `ObjectCache` (64 MB default, ~130,000 objects at 512 B each, least recently
used let go first; the same `BudgetCache<T>` as `TerrainCache`).
- **The file**: a 20-byte header ("FOBJ", version, block count, table offset), the blocks, then the table: the space
  ids as a string table; per block its space, chunk, offset, length, object count, codec, and the 64-bit FNV-1a hash
  of each object's id; then the override ids. Opening reads the header and table (a few bytes per object), which is
  what answers `Keys`, `CountOf`, `TryLocate` (by hash; two ids with one hash is a vanishingly rare thing, and the
  first block to name it keeps it) and `Overrides` without reading a block. Blocks are sorted by key.
- **The codec** (`ObjectCodec`): per block a string table of namespaces and base ids, then per object its namespace
  index, name, base id indices, position, rotation and scale as plain floats: ~64 bytes an object. Decoding checks
  that every object stands in the block's chunk and space, belongs to the plugin or a master, and that ids are sorted
  and unique; so a block that breaks the plugin's rules fails when first read, not at load. A block where some object
  wears other materials is codec 2 (`ObjectCodec.WithMaterials`): after its scale each object lists its overrides
  (a count byte, then slot, material namespace and material name as string table indices); every other block stays
  codec 1 (`Plain`), byte for byte what it was, so files without materials never change. The table stores each block's
  codec, and a saved-again file copies it with the packed block.
- **Saving** (`PluginFileStore.Save`): the objects file is written like the terrain file (blocks still stored in another
  objects file are copied packed with their hashes), named by its content, then the JSON names it; older objects files
  go after the commit, except the one the plugin was loaded with.
- Measured on the stress world (1,024 chunks, 24,576 objects): opening its plugins now reads 282 KB of JSON and the
  side files' tables in **94 ms (was ~1,100 ms)** and adds **2.3 MB (was 14 MB)**; resolving 4 ms, indexing the space
  3 ms, and reading the 13 × 13 chunk window around the camera (3,840 objects in 160 blocks) 44 ms.

**Known v1 limits** (deliberate, revisit when needed):
- No field-level merge between conflicting overrides.
- Conflict awareness only checks *direct* masters, not transitive ones.
- Two namespaces defining the same chunk coordinate: the world view shows the
  lowest id and logs a warning.
- Sculpting only edits existing chunks and never creates new ones. Upright objects follow the ground
  by its height change under their pivot; slope objects (fences, rocks) are also re-tilted.
- Beyond the loaded chunks the land is coarse far tiles (8 m samples, no collider, nothing on them: no objects, no
  water depth, no picking); past the far radius the fog hides everything. Far land lags a sculpt stroke until the
  stroke commits, and sits 2 m under the real land, so a ditch dug deeper than that between two 8 m samples can show
  it through until the tile rebuilds. Far tiles' normals are per tile, so a faint lighting seam can show on tile edges.
  Terrain heights and placed objects both load a chunk at a time; the records in between (base objects, spaces,
  climates, lakes, the small terrain chunk records) load whole, which is fine until there are hundreds of thousands.
- An object's old chunk is only refreshed in the scene when that chunk has been shown: `ChunkIndex` learns where an
  object is as it serves chunks, so a change to an object in a chunk nobody has shown dirties only its new chunk.
- Conflicts between masters' placed objects are judged by id through the block locators (hashes), so an override of
  an object only a damaged block would reveal is judged as if it existed.
- Chunks edited in a session keep their heights in memory until it ends, even after saving.
- The sea is one level for the whole worldspace, so any hollow below it fills, inland or not. Picks go through water
  to the ground beneath, and placing under water is allowed.
- A lake whose shore is dug away (so its water would run off) **spills** and shows no water until the shore is rebuilt
  or it is removed; the Water tool still picks it near its point. Lakes don't follow a sculpt stroke live, only once it
  is committed. `WaterMap.Of` fills every lake of the space each time it is built (per relevant world change).
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
  override in the active plugin. `ObjectEdits.Delete` deletes many objects as one step. `ObjectEdits.SetMaterial`
  (one slot of many objects, a material or null for the base look) and `ResetMaterials` (every slot) share the same
  commit path: one undo step, unchanged objects skipped, transforms keep an object's materials.
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
- `WaterMap` (`Editing/Water`, engine-free) is **the one water question**: `SurfaceAt(x, z)` (null where dry; the sea
  level beyond the land) and `DepthAt(x, z)` (0 where dry, null without terrain). The commands, and later the player
  (swimming) and NPCs, ask it, never the scene; `LakeAt(x, z)` names the lake there. The highest water wins (a sea
  above a lake covers it). `SpaceEdits.SetWaterLevel` sets or removes a sea as one undo step (overriding a master's
  space in the active plugin).
- **Lake fill** (`LakeFill`, engine-free, 4-neighbour on the global sample grid, capped at `MaxSamples` = 128 Ki samples):
  `Fill(point, level)` floods from the sample nearest the point to every connected sample below the level
  (`LakeExtent`; it `Spills` when it reaches the land's edge or the cap). `Survey(point)` is a priority flood (always
  step to the lowest sample around what is reached, `Forge.Core.Collections.MinHeap`): the highest ground crossed on
  the way to the land's edge is the hollow's **brim**, the lowest point of its rim, so any level up to it stays in
  (`LakeBasin`; `FullLevel` = a quarter meter under the brim, where new lakes start). `LakeExtent.CellRuns` gives the
  cells with a wet corner as runs per row, one quad each when drawn. `WaterEdits` adds, re-levels and removes lakes,
  one undo step each, refusing a dry point or a spill with the brim's height.
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
- **Freezing** (`EditSession.Freeze(reason)` → a handle; `IsFrozen`): the world is read-only until the handle is
  disposed. Every edit, batch, undo, redo and new draft throws, naming the reason, so whatever runs meanwhile can read
  the world but never write it. One freeze at a time, refused inside a batch or with a draft open. Playing freezes it.
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
    east), the water when there is a sea (its level, the share of samples under water and the deepest, from every
    sample, through `BuildContext.Water`), and the lakes reaching into it (id, point, level, whether it spills), and
    the objects inside, nearest first. It reads the terrain *data* (`BuildContext.Terrain`, a
    `TerrainGrid` cached per world version), so it answers for unloaded chunks too; placing still seats on the
    loaded scene, and a skipped spot says whether its terrain is missing or just not loaded.
  - `set_sea_level`: sets or removes the worldspace's sea (refused in interiors) and reports how many objects now
    stand below it. The instructions tell the agent to use it only when the user asks.
  - `set_climate`: gives the worldspace a climate by name or id, or removes it (refused in interiors; an unknown one
    is refused with the list of climates), and reports the year and the daylight at midsummer and midwinter.
  - `add_lake` (x, z, optional level, default the hollow's full level), `set_lake_level`, `remove_lake`: lakes through
    `WaterEdits`; refusals say how high the water can go, or to dig a hollow first (`flatten_area` lower).
  - `flatten_area`: levels a rectangle (`AreaFlattening`) to a height, by default the average ground height
    inside it (as much dug away as filled in), with a soft edge (default 4 m); objects on the reshaped ground
    follow it (`BuildContext.FollowTerrain`). The agent levels sloped building sites before placing on them.
  - `place_objects`: objects at ground positions, seated on the terrain; heading and scale default to the
    object's natural variation. Unknown ids refuse the whole call; positions without terrain are skipped.
  - `place_line`: one object end to end along a path, stretch by stretch, exactly like the place tool's
    Shift line (`PlacementStroke`); corners shared by two stretches get one object; `closed` encloses an area.
  - `place_outline`: one object around a rectangle (x, z, width, depth, heading; walls, fences) or with `fill` over
    it (floors), exactly as the outline tool lays it (`RectLayout`); pieces without ground are skipped.
  - `adjust_objects`: moves, turns and resizes placed objects by id; what an item leaves out stays as it is,
    and each object is re-seated on the terrain by `ObjectSeating` at its new spot, then committed through
    `ObjectEdits.Transform` as one step. Unknown ids and spots without terrain are skipped and reported.
  - `list_materials` (id, name; optional search) and `set_materials`: one slot of placed objects (ids) in a material,
    or back to their own look when the material is left out, one undo step through `ObjectEdits.SetMaterial`; objects
    without the slot are skipped with the slots they have. `list_objects` names each object's slots and
    `PlacedObjectInfo` (inspect, adjust) what an object wears. Slots reach Editing through `BuildContext.SlotsOf`, which
    the editor feeds from `AssetSlots` (EditorApp: each asset key's `MaterialSlots` read once from a throwaway
    instance, the same source the selection options use, so loaded or not, every caller sees the same slots).
  - `list_grounds` (id, name, which one unpainted land shows; optional search), `paint_line` (a band `width` m wide
    along points, rounded ends, `closed` to loop) and `paint_area` (a rectangle as `flatten_area` takes one, or a
    polygon by its corners): ground paint through `GroundPainting` (Editing/Terrain), one undo step, in the draft.
    The ground is named by id or by name (any case). Unlike the brush's dabs, which build up, a shape paints each
    texel once by its signed distance to the outline: full inside, fading over a soft `edge` centered on the outline
    (so a 3 m path looks 3 m wide), times `strength` (under 1: worn ground). `GroundPainting` and `PaintStroke` paint
    through one `PaintCanvas` (working copies of the touched chunks' paint) and commit through
    `TerrainEdits.Commit(session, IPaintEdit)`. The unpainted ground reaches Editing through
    `BuildContext.UnpaintedGround` (the editor's `EditorContext.UnpaintedGround`); without one, painting is refused,
    as the paint tool refuses.
  - `delete_objects`: placed objects by id.
- The editor actions that predate the registry (drop to ground, frame, undo/redo, save, the tools' gestures)
  are not commands yet; each moves into the registry when the agent needs it.
- **Headings are compass bearings of the object's front**: 0 north (+z), 90 east (+x). That is the engine's
  yaw, because every asset's front faces local +Z (Unity's forward).

## Streaming (Forge.Streaming + Forge.World)

- `ChunkIndex` (engine-free) groups the presentable records by chunk: a terrain record per
  coordinate (lowest id wins on duplicates), indexed up front, and placed objects by the chunk under their
  position (`WorldGrid.ChunkOf`), which are the store's and read a chunk at a time when a chunk is shown
  (`IRecordStore.ObjectsIn`), so building the index reads no objects.
- `ChunkWindow` (engine-free) plans loads and unloads around a focus chunk: load within
  `LoadRadius`, unload beyond `UnloadRadius` (the gap is hysteresis), loads nearest first.
- `WorldView` presents **only loaded chunks**. `LoadChunk` / `UnloadChunk` add or remove a
  chunk's records. Loaded coordinates include empty ones, so content added there later appears.
- `WorldView.Sync(world)` is **incremental** when `ResolvedWorld.TryGetChangesSince` knows the
  changed ids, which it does when both worlds layer over the same `MasterLayer`.
  `ChunkIndex.Update` re-indexes just those ids and returns the dirty chunks, and only loaded
  dirty chunks are re-presented. A change to a dependency (a base object, which many placed objects
  depend on) falls back to a full rebuild; records the view doesn't present (spaces, lakes) are skipped.
- `WorldStreamer` follows the camera (a world-coordinate focus). It unloads at once and loads within a per-frame time
  budget (`EditorAppBootstrap`: radius 6 / 8 chunks, 4 ms). A new chunk starts only if the
  average build time still fits. Terrains use `allowAutoConnect`, so LOD edges stitch.
- **Origin shifting.** Records keep true world coordinates however big the map; the scene is laid out relative to a
  `SceneOrigin` (engine-free): the corner of one chunk, moved to the focus's chunk once the focus strays more than
  `_rebaseDistance` chunks (8) from it, so rebases are rare and by whole chunks. `WorldFrame` makes the world root
  (the `EditorApp` object) that frame: everything that stands in the world (terrain, objects, water, the interior
  floor, gizmos, ghosts and ground lines) is a child of it at its world coordinates, so a rebase is one root move
  (`Follow`), and `Shifted` tells the camera, which is not under the root, how far to move (`FlyCamera.Shift`, glide
  included). Heights are never rebased (the origin's y is 0). The frame's two conversions are the only bridge between
  scene coordinates (camera rays, physics) and world ones: `ScenePicker` takes world rays and returns `WorldHit`s, the
  controller converts the camera's ray once into `ToolInput.Ray`, and `SpaceCameras` remembers world poses. Tools,
  commands and the agent never see scene coordinates. The thumbnail rig is deliberately *not* under the root.
- **Far land (distant LOD).** Past the loaded chunks the land is drawn from the records as coarse meshes: `FarTiles`
  (engine-free) cuts the land into tiles of 4 × 4 chunks (256 m) and samples each every 8 m straight from the chunks'
  own samples (every 8th at 1 m, every 4th at 2 m: never interpolated, so tile edges and chunk edges share exact
  heights), into an immutable `FarTile` (heights plus which chunks exist, so missing chunks are holes, not land at 0).
  `FarTerrainView` (World) streams the tiles with a `ChunkWindow` over *tile* coordinates (`_farRadius` 16 chunks →
  4 tiles ≈ 1 km; the fog hides everything one tile short of that, `FogReach`, so the outer ring's stepped edge never
  shows against the sky), under the world root like everything else, and `FarTerrainMesh` builds a plain
  mesh per tile with no collider (`ScenePicker` never hits far land). Tiles cover the near land too, 2 m below it, so
  the real chunks draw over them and there is no seam to stitch at the load radius. Where the land ends (a chunk
  side with no chunk beyond it, `FarTile.Edges`, sampled at the chunk's own resolution) the tile hangs a skirt from
  the real terrain's edge to 4 m under the far land, in its ground's color in shade, so the map's edge is a solid
  slab, not two thin sheets with sky between them; `Invalidate` rebuilds the neighbouring tile on every side, so an
  edge that stops being one (extend land) loses its skirt. `WorldStreamer` gives the far
  land whatever budget the chunks left in the frame (one tile anyway when no chunk loaded, so it never stalls);
  tiles rebuild when `WorldView.TerrainChanged` names a chunk of theirs (a sculpt commit, a paint stroke, extend land,
  undo), edge neighbours included. A tile reads its 16 chunks through the terrain cache, ~2.5 ms worst case here.
  Each far sample also records the ground showing most at the paint texel under it (`FarTile.GroundAt`, null where
  unpainted), and the mesh carries it as a vertex color: the layer's map color (`GroundLooks.MapColorOf`, the
  average of its color texture, linear, stored in the catalog by the build tool). `Shaders/FarTerrain.shader`
  (`Forge/FarTerrain`, on `Materials/FarTerrain.mat`) lights those colors with the sun and the sky's ambient and
  fogs them like the near land, with a depth pass so the water reads its depth next to far shores.
- **Grass** grows from the paint: a ground layer may say what grass it grows (`GroundLayerRecord.Grass`,
  `GrassSettings`: tufts per m² where painted fully, height; optional, so bare layers and old plugins are unchanged).
  `GrassScatter.Grow` (Streaming, engine-free, tested) turns a chunk into tufts: per paint texel, each layer with
  grass grows its density times its share (unpainted ground counts as the unpainted layer), standing on the
  bilinear ground, none below the sea, seeded per texel so a chunk always grows the same grass. `GrassView` (World)
  keeps a patch per chunk within the material's fade end (45 m) of the camera, each a `GraphicsBuffer` of 32-byte
  tufts drawn with one `Graphics.RenderMeshPrimitives` of `GrassTuftMesh` (6 blades, 18 triangles): no game objects,
  no colliders, so picking and seating never hit grass. A patch is regrown when its chunk's terrain record or the
  sea level changes (records are immutable, so a reference check per frame), nearest first in the frame budget.
  Patches draw through the world root's matrix, so a rebase is free. `Shaders/Grass.shader` (`Forge/Grass`, on
  `Materials/Grass.mat`) reads the buffer by instance id, sways tips with the wind, shrinks blades into the ground
  between the fade start and end so the edge never pops, colors each tuft its ground's map color (root darker, tip
  lighter, a little variation), lights it like the far land with up-facing normals, and has a depth pass.
  `EditorGrassSource` feeds it the shown land, the world's ground layers and the active sea; lakes don't stop grass
  yet. Measured here: ~100k tufts (1.8M triangles) around the camera on unpainted land; a 20k-tuft patch takes
  ~10 ms to grow in the editor (see Shortcuts).
- Ground layers are matte: the build tool scales each mask map's smoothness into [0, 0.35]
  (`TerrainLayer.maskMapRemapMax.w`), and material balls use the same value, so scanned grass has no wet sun glare.
- Measured on the stress world (25,394 records: 1,024 chunks = 2 × 2 km, ~24k objects,
  30 MB JSON), with Unity unfocused, so this is CPU work only and excludes render and destroy cost:
  - startup: load plugins 1,312 ms, resolve 9 ms, first view 211 ms (113 chunks, 2,615 objects)
  - flying at 80 m/s: streaming p50 3.0 ms / p95 4.6 ms / max 9.4 ms per frame, no frame over 16 ms
  - chunk build: ~2.7 ms on average, never more than 12 chunks of backlog
  - one edit (move + undo): **p50 0.04 ms, p95 0.11 ms** with layered resolution and incremental
    index updates. Before that it took 22–55 ms (a full resolve plus a full index rebuild per edit).
- The panel's **Performance** foldout shows these numbers live: chunks, build times,
  worst frame and hitches (>50 ms), last edit and startup phases.

## The player (Forge.Game + Forge.Play)

The game's rules are engine-free (`Forge.Game`, tested with a fake body); `Forge.Play` gives them the engine's body and
controls, as Streaming and World split the streaming. First and third person, as in Skyrim: the look is kept apart
from any camera, so both cameras read the same player.

- `PlayerCharacter` is the player in the world: its `PlayerLook` (a compass heading, which also turns the body, and a
  pitch up positive, held within ±85°), its `PlayerLocomotion` and its body, an `IPlayerMotor` (feet position, `Move`
  that says what it touched in a `MotorContact`, `Teleport`). `Tick(PlayerCommands, dt)` turns the look, steps the
  rules and has the body make the move; `PlaceAt(feet, heading)` stands it somewhere at rest. `Eyes` = feet + eye height.
- `PlayerCommands` are the only input the rules see (move: x right, y forward in [-1, 1]; turn in degrees; sprint
  held; jump and toggle-walk pressed this frame), so a gamepad, a script or a test drives the player alike.
- `PlayerLocomotion` (`LocomotionSettings`: run 4.5 m/s, the default pace as in Skyrim, walk 1.8 while
  walking is toggled on (Caps Lock, as Skyrim's, kept between spawns), sprint 6 over either (faster and the clips show it as fast-forward), a 1.1 m jump, gravity
  18 m/s², ground and air acceleration, a 50 m/s fall cap, 0.12 s coyote time): on the ground the pace eases toward the
  wanted one in about a tenth of a second and the body is pressed into the ground (`GroundStickSpeed`) so it stays on
  downhill slopes; in the air gravity pulls and steering is weak; a head against a ceiling stops rising; one jump per
  landing, still allowed just after stepping off an edge. Diagonals are no faster. Headings turn into directions through
  `Forge.Core.Geometry.Compass` (also behind `PlacementMath`).
- `CharacterControllerMotor` (Play) is the body: Unity's `CharacterController` sized from the profile (1.8 m tall,
  0.3 m radius, 0.4 m steps, 50° slopes), its own scene object on the Ignore Raycast layer, so the editor's picks and the
  camera's probes look through it. It collides with the terrain and the placed objects' colliders.
- `PlayerControls` (Play) read the profile's action map (`Settings/InputSystem_Actions`, map Player: Move, Look, Sprint,
  Jump, ToggleWalk, ToggleView, Zoom; WASD and arrows, mouse, Shift, Space, Caps Lock, F, the wheel, and the gamepad) into `PlayerCommands` and
  `ViewCommands`; the look is pointer pixels × the profile's sensitivity (0.1° per pixel), and the wheel's binding scales
  its steps (120 a notch) to notches with a Scale processor. No key is named in code.
- `PlayerProfile` (`Settings/Player/PlayerProfile.asset`) holds the body, the pace and the controls, tuned in the
  Inspector.
- **Body facing** (`BodyFacing`, `PlayerCharacter.BodyHeading`): the body's own heading, which its model is turned to.
  Facing the `Look` (first person) it is the look's heading; facing its `Movement` (third person) it turns toward the
  ground velocity at `LocomotionSettings.TurnRate` (720°/s) the short way round, and keeps its heading while standing,
  so the look orbits freely, as in Skyrim. Movement is always relative to the look, so stepping right in third
  person runs right and the body turns to it.
- **Cameras**: an `ICameraMode` (Game, engine-free) says where the camera goes each frame (a `ViewPose`), how the body
  faces meanwhile and whether the body shows. `FirstPersonCamera`: at the eyes, turned to the heading and tilted by the
  pitch, the body hidden. `ThirdPersonCamera` (`ThirdPersonSettings`: 3.5 m behind, 1.2 to 8 m on the wheel, 0.6 m a
  notch, orbiting a pivot 1.6 m up and 0.35 m right of the body): along the look behind the pivot; an `ICameraObstacles`
  (`PhysicsCameraObstacles`, Play: a sphere cast of the camera's 0.2 m radius against the default raycast layers, so the
  terrain and placed objects, never the player's body) pulls it in at once when something is in the way, and once clear
  it eases back out (6 m/s); the shoulder offset is checked too, so a wall beside the player keeps the pivot in.
  `ViewSwitch` holds both, as Skyrim switches them: ToggleView (F) swaps them, the third-person distance kept; the wheel
  (Zoom) moves it, one notch nearer than its closest is first person, one notch out of first person is third at its
  closest. `CameraDirector` (Play) puts the engine's camera where the current mode says.
- **The player while playing** (`PlayerSession`, Play): the body, the model, the controls, the rules, the view, what it
  has in sight (`Focus`, an `InteractionFocus`) and what it carries (`Inventory`), all built from the profile.
  `Spawn(feet, heading, world, interactions)` makes the body and model, stands the player, gives it the camera, switches
  the controls on and captures the mouse; `Tick` applies the view commands, sets the body's facing from the current
  view, steps the player, then frames it and turns and shows (or shadows only) the model, and last looks for what is in
  sight from where the camera now is (`CameraDirector.Pose`), so the prompt and the use key go by what the frame shows,
  and uses it if the key was pressed; `Despawn` takes it away. One per app, so the view chosen, the attributes and what
  it carries carry over to the next spawn.
- **The use key in play**: the Player map's `Interact` (E, the gamepad's north button; a tap) is
  `PlayerCommands.Activate`, and `PlayerControls.InteractKey` names it for the prompt (its binding in the actions' first
  control scheme, the keyboard's). `PhysicsInteractionProbe` is the line of sight: a ray, then a ball of the profile's
  `AimRadius` (0.1 m) along it, on the default raycast layers (never the player's body), triggers ignored, a placed
  object known by its `RecordLink`. The profile's `Reach` is 2.5 m from the eyes.
- **The game while playing** (`GameSession`, Play): one running game: the stream (`GameEvents`), what playing changed in
  the world (`WorldChanges`), what the use key can do (`Interactions`: take) and the player (`PlayerSession`).
  `Begin(feet, heading, world)` spawns the player, `Tick` plays a frame and then flushes the stream, `End` takes the
  player away and lets the world's changes go. A placed object taken out of the world leaves the scene
  (`WorldView.Hide`, which also keeps it out when its chunk streams in again) and the focus looks again; when the
  changes are let go, `WorldView.ShowHidden` puts everything back. The editor's play from here runs one; the game app
  will run one as it is.
- **The model** (`PlayerAvatar`, Play): the profile's `_model` as a `CharacterModel` (any character's look on any body:
  the prefab riding on the body's object, turned to the body's heading, its colliders removed since the body is the
  collider, on the body's layer, animated from a `BodyMotion`; the world's characters will use it too), plus the
  player's own extras: in first person its renderers cast only shadows, and its animator always animates. The player is the **Viking hero** (`Content/Characters/VikingHero`, 1.85 m, eyes at 1.72 m), the first
  real character, from the `blender-tests` art repo (see its CREDITS.md). Before him it wore a placeholder mannequin
  (`player_mannequin`, built from primitives by `PlaceholderCharacters` through *Forge → Generate Placeholder Player*,
  which gives it to a profile with no model); it stays as the fallback.
- **Characters** (`Content/Characters/<Name>/`): the rigged model `<Name>.fbx` (feet at the origin, facing +Z, meters),
  its colour `<Name>_Color.png` and optionally `<Name>_Normal.png`. `CharacterImport` (an `AssetPostprocessor`) imports
  every model there as a **humanoid** with its own avatar, so a Mixamo skeleton maps to Unity's human rig and any
  humanoid animation plays on it, and with no materials, cameras or lights. *Forge → Characters → Build Characters*
  (`CharacterBuilder`) makes each folder's URP Lit material (2048 px textures, matte) and prefab: a variant of the model
  (a re-exported model updates it) wearing the material, its skeleton put back in the bind pose (a Mixamo download
  stands in its animation's first frame), with an `Animator` on the avatar, no root motion (the body moves it) and
  culled transforms off screen. A new character is a folder dropped in and a rebuild. The models and textures are plain
  git objects (`.gitattributes`), as the ground's are.
- **Animations** (`Content/Animations/<Set>/<Move>.fbx`): one Mixamo clip per move (FBX for Unity, 30 fps, without
  skin, travel left in), named after its move (`HumanoidMoves`: ground moves Idle, Walk, Run, Sprint blended by speed;
  Jump, Fall, Land in the air). `AnimationImport` imports each as a humanoid clip read against one T-posed Mixamo
  skeleton (`ReferenceRig`, the hero's today: a skinless file has no rest pose and Unity would take its first frame,
  mid-stride, for one, turning the legs out), loops the repeating moves, bakes turning into the pose and keeps travel as
  root motion, which the body ignores but which gives each clip's own speed; a jump's rise is left out of its pose (the
  body's rise shows it). *Build Characters* first builds each **move set**'s `<Set>.controller` (`HumanoidAnimatorBuilder`):
  `Humanoid` is the base every character plays, and another set (a guard's, an elder's) holds only the moves it
  changes, the rest coming from the base; a whole controller per set rather than an override, since its clips walk at
  their own speeds. A character plays the set its optional `<Name>.json` names (`{ "moves": "Guard" }`). Each is built
  from the moves present: the ground moves blended at their clips' speeds; a jump when the body leaves the ground going
  up, a fall when it drops past 3 m/s, a landing when it touches down, each optional. A new move is a file dropped in
  and a rebuild.
- **How the body moved** (`BodyMotion`, `Forge.Game.Motion`, shared by anything with a body): `PlayerCharacter.Motion` each frame, from where the body actually went
  (ground speed, vertical speed, grounded), so a run into a wall shows standing. `StrideMatch` keeps the feet planted:
  the speed in the clips' own size (the avatar's `humanScale`) picks the blend, and past the fastest clip it plays
  faster. `HumanoidAnimation` (Play) is the one thing feeding an `Animator` (Speed damped 0.1 s, Pace, Grounded,
  VerticalSpeed; TopSpeed is the builder's default); `PlayerAvatar.Animate` drives it, and the player's animator always
  animates, so its first-person shadow walks too.
- **Play from here** (`PlayFromHere`, EditorApp, built by the controller when the bootstrap has a `_player` profile):
  when the app starts playing, it begins its game (`GameSession`) with the player on whatever the middle of the view
  shows (else the ground under the camera, else it falls from the camera), facing the camera's heading, and rests the
  fly camera (`enabled = false`). The controller ticks it each frame while playing; Esc ends the game (the player goes,
  and the world is as built again: what was taken is back, as in the Creation Kit's test play, while the player keeps
  what it carries) and wakes the fly camera, and the bootstrap gives the editing camera back. The camera is the streaming's focus, so the streaming and the
  origin follow the player; a rebase moves the player with the camera (`PlayFromHere.Shift`). The world's edge is open:
  walking off the land falls forever (Moti's call, as it stands).
- Known limits: interiors have no floor to stand on yet (the floor grid has no collider), there's no swimming (water
  has no colliders, the player walks the bottom), and the gamepad's look stick is read like the mouse (per frame,
  not per second), so it turns slowly. The prompt names the keyboard's key even when a gamepad plays, and a pile of gold
  is one gold until placed objects carry a count.

## Attributes (Forge.Data + Forge.Game.Attributes)

Skyrim's actor values, built to grow: what an actor has a number for is data, and what changes the numbers names
where it came from.

- **`AttributeRecord`** (`attribute`, `<ns>:attribute/<name>`): a `Resource` (a pool spent and refilled: health,
  stamina, magicka) or a `Stat` (a number: carry weight, a skill, a cost); a base value, a min and an optional max;
  `Terms`, what other attributes add to its base (`AttributeTerm`: "+0.5 per stamina", reading the other's full value);
  and for a resource the stats holding its regen rate (points a second) and delay (seconds after being spent). A mod
  adds an attribute with a record or retunes one by overriding it; no code.
- **The base game's set** (`CoreAttributeSet` in the generator, ids in `CoreAttributes`, the ones the game's rules
  read by name): Health, Stamina, Magicka (100 each; regen 1, 10 and 3 a second after 5, 1.5 and 2 s), Carry Weight
  (250 + 0.5 per stamina), Sprint Cost (10 stamina a second), Jump Cost (10), Safe Fall Height (4 m), Fall Damage
  (10 health a meter past it). Costs are attributes, so boots that make sprinting cheaper are just a modifier.
- **`AttributeSet`**: the resolved attributes, checked once (every name read exists, regen reads stats, terms never
  go round in a circle; every problem in one `DataValidationException`) and shared by every actor: slots, who reads
  whom, and an order where each comes after what it reads.
- **`ActorAttributes`**: one actor's numbers over the set, owned by an `ActorId` (the player's now, any NPC's later). Value =
  `(base + terms + adds) × (1 + percents/100) × multipliers`, within min and max. Each `AttributeModifier`
  (`Add`, `Percent`, `Multiply`) is added under a `ModifierSource` (an item worn, an effect, a perk); `Remove(source)`
  takes exactly its modifiers away, and a modifier given seconds wears off by itself. Values are worked out when
  something changes, rippling in order to whatever reads them, so reading costs nothing. A resource stores the
  damage it took, not what is left: `Current` = value − damage, so a bigger pool lifts what is left and a smaller one
  never leaves more than it holds. `Drain` takes what there is, `TrySpend` all or nothing, `Restore` gives back; any
  spend holds the refill back for the regen delay. `SetBase` gives the actor its own base (a level gained). Its own
  events: `Changed` (value or what is left, often: for what draws the numbers), `Depleted` and `Refilled`.
  `AttributeEvents.Bind` puts the happenings on the game's stream as `ResourceDepleted` and `ResourceRefilled`.
- **What moving costs** (`MovementCosts`, Game.Player, the player's today): sprinting spends `SprintCost` stamina a
  second while moving and running dry leaves the actor **winded**: it walks, whatever it is told, until a quarter of its stamina is back;
  a jump needs and spends `JumpCost`; a fall (`FallTracker`, Game.Motion: the highest point off the ground less where it
  touched down, moved with a rebase) past `FallSafeHeight` costs `FallDamage` health a meter. `PlayerCharacter` takes
  the actor's attributes, has the costs filter its commands and pay for the move, and ticks the attributes. The costs
  tell the stream `ActorExhausted`, `ActorRecovered`, `ActorShortOf` (a jump without the stamina) and `ActorFell`
  (only a fall that hurt: how far, how much). At zero
  health nothing more happens yet. `PlayerSession` keeps the player's attributes from one spawn to the next, and starts
  them afresh only when the world's attribute records changed; a world without attributes plays as before.
- **The HUD** (`PlayerHud`, Forge.Play.Hud; the game's, which the editor hosts through `HudSection` while playing):
  Skyrim's magicka, health and stamina bars along the bottom (`AttributeBar`, styled by UI/Hud/PlayerHud.uss, imported
  by the runtime theme; a bar's kind is a style class, so a mod's bar is a style and one layout entry). `BarFade`
  (Game.Hud, engine-free) decides when each shows: while not full, then 2.5 s after it fills or changes, then a fade;
  the stream's `ActorExhausted` and `ActorShortOf` make a bar blink. A dot marks the middle of the view, lit while
  something in sight can be used, and the use key's prompt sits under it (`InteractionPrompt`: the name, the key and
  the verb, the detail line; from the player's focus, redrawn only when the offer changes). It follows whichever player
  the session has, rebinding on a new spawn.
- **Live numbers are game state, not world data**: an actor's damage, own bases and timed effects belong to a save
  game, never to a plugin; modifiers from equipment are rebuilt from what is worn, so each fact lives in one place.

## Game events (Forge.Game.Events)

The game's backbone for "something happened": the teller never knows who listens. `GameEvents` is one stream per
running game (the editor's play from here owns one today; the game app will own its own).

- **The rule: values stay local, happenings go on the stream.** Numbers that change every frame (a bar refilling)
  stay on their owner's own C# events, read by the one thing that draws them. What happened, rarely and to many
  listeners (a resource ran out, an actor fell, later an item taken, a crime seen), goes on the stream.
- **Events are facts**, past tense (`ActorFell`, never `HurtActor`), small readonly structs implementing `IGameEvent`
  (so telling makes no garbage), each naming its actor (`ActorId`, Game.Actors; `ActorId.Player` is the player, a
  placed actor will be its record's id). Events live beside what tells them (`AttributeEvents.cs`,
  `MovementEvents.cs`).
- **`Publish` only queues; `Flush` tells**, in publish order across all types, and the loop owner flushes once a
  frame after the frame's rules have run (`PlayFromHere.Tick`, and on entering or leaving play), so a listener never
  runs inside a half-done step. What listeners publish is told in the same flush, after what came before it; a chain
  that never ends stops after `MaxPerFlush` (10,000) and is reported.
- **`Subscribe<T>`** returns a handle; disposing it stops the listener at once, even mid-flush. Listeners hear in the
  order they subscribed; one that throws is reported (the engine's log in play; thrown together from `Flush` when no
  reporter is given, as in tests) and the others still hear.
- **`SubscribeAll`** hears every event, boxed, after its own listeners: for logs, recordings and, later, the memory of
  the world's characters (what they saw happen). Not for gameplay.
- Lookups are an array read per event type (each type gets a small index once), listener lists are copied on change,
  queues keep their capacity: no allocation in steady play. Not thread-safe; it belongs to the game's loop.

## Interaction and items (Forge.Game.Interaction, .Items, .World)

Skyrim's use key: look at something within reach, a prompt says what it is and what the key does, and the key does it.
Engine-free and tested with a fake probe; the doing is the same for the player and, later, the world's characters.

- **What the key can do** is an `IInteraction`: it makes an `InteractionOffer` for an `InteractionTarget` (a placed object
  and its base object) or passes, and `Perform`s what it offered. `Interactions` holds them in order, one per running
  game, and the first to offer is what the key does (one verb per thing). Each reads its own part of the base object, so
  doors, containers and talking will be one interaction (and part) each; nothing else changes. An offer is what the
  prompt shows: the target's name, the verb, an optional detail line.
- **What is in sight** (`InteractionFocus`, one per actor): each frame it takes the stretch of the view's middle line the
  actor can reach (`ReachRay`: from level with its eyes, so nothing between a third-person camera and the actor counts,
  out to where the line leaves the reach of its eyes; in first person simply the first meters of the view), asks an
  `IInteractionProbe` what that stretch could mean (the object it hits, then one it grazes, so a coin needn't be hit
  dead on; nothing behind the first thing in the way) and keeps the first some interaction offers for. It asks again only
  when what is in sight changes (records are immutable: a changed record is another one) or on `Refresh`, so steady
  looking costs nothing. `Activate` does what is on offer and lets go of it.
- **Taking** (`TakeInteraction`, verb "Take"): a placed object whose base object has an `ItemPart`; the prompt's detail
  is its weight and value. It leaves the world (`WorldChanges`), joins the actor's `Inventory`, and the stream hears
  `ItemTaken` (who, the item, the placed object it was, how many).
- **`WorldChanges`** (Game.World): what playing changed in the world, beside it, as a saved game keeps changes over the
  load order: the world's placed objects taken out of it (`Removed`) and the ones playing put in (`Added`: a dropped
  item, ids `forge.play:placed/N` from `NewId`, never given twice). Taking an added one simply ends it; `Clear` lets
  everything go. The records stay as built; opened doors and emptied containers will join it, and the save game will
  write it.
- **`PlayedWorld`** (Game.World): an `IRecordStore` over the world as built with the changes on top, live: what was taken
  is gone from it, what was dropped is in it, an unchanged block is handed on as built. Whatever asks about the world
  while playing (what is in sight; later the AI and the save) reads this, so a dropped item is takeable again.
- **`Inventory`** (Game.Items): a stack per item (`ItemStack`: base object id and count) in the order first taken; one per
  actor; `Changed` on every add and remove. `Weight(records)` and the listing are read from the items' records when
  asked (`ItemRecords.TryGet`: a base object with an item part), so they are never stale; an item the world has no
  record of stays carried but is neither weighed nor listed.
- **`InventoryList`** (Game.Items): an inventory as a screen lists it, by name, all kinds or one (a tab): `InventoryEntry`
  rows with name, kind, count, weight and value each, and the stack's total weight.
- **Carrying** (`Encumbrance`, Game.Items): what an actor carries against its `CarryWeight`, worked out again only when
  the inventory or the carry weight changes. Past it the actor is overburdened, as Skyrim's over-encumbered: it walks
  whatever it is told (no run, no sprint; jumps still go), and the stream hears `ActorOverburdened` / `ActorUnburdened`.
  It is an `IMovementLimit` (Game.Player): `PlayerCharacter.Limits` let each frame's commands through, in order, before
  moving costs are paid, so the player's rules never know what holds it back (a spell's root, a wound, later).
- **Dropping** (`ItemDropper`): one item leaves the actor's inventory and stands in the world as an added placed object
  where the caller says (the engine finds the ground), and the stream hears `ItemDropped`. The record is made before
  the item leaves the pack, so a bad place loses nothing.
- `PlayerCommands.Activate` is the key, pressed this frame; `Allowing(sprint, jump, walk)` is a command as a rule lets it through.

## World-building agent (Forge.Agent)

The user describes what to build ("a fenced house surrounded by trees") and the agent builds it with the
world-building commands. **The model plans; deterministic commands do the geometry** (seating, spacing,
slopes), so results are exact and every edit goes through the edit session.

- **Tools**: `ToolInvoker` turns the command registry into tools: `ToolSchema` builds each JSON schema from
  the `[Param]`s, arguments bind from snake_case JSON (missing or unknown arguments are errors), results go back
  as snake_case JSON. Bad arguments and refusals come back as error results the model reads and corrects.
  A result that brings pictures back (`IPictureResult`) keeps them out of its JSON; they ride on the `ToolResult`,
  go to the model as MCP image blocks after the text, and show on the tool's line in the chat.
- **Look**: the agent's eyes are `look` (`LookCommand`, EditorApp, registered by `AgentHost` beside the building
  commands, since it needs the engine). With no arguments it pictures the user's view; with `x`, `z` a ground point
  from `distance` m, facing `heading`, down by `pitch` (90: plan view). `AgentEyes` moves the editor camera for the
  one render (`AgentPictures.Snapshot`) and puts it back, so the user's view never moves. The build under review is
  the selection, so it shows highlighted. Only what the streaming has loaded is drawn: views far from the user's camera
  lack the grass and show the far land's coarse tiles. The instructions ask for a few looks after building, not one per step.
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
  the message goes in on stdin as one stream-json user message (`--input-format stream-json`, `ClaudeInput`: the
  turn's pictures as base64 image blocks, then the words; `CommandLine` quotes the rest); follow-ups `--resume`
  the conversation, which keeps the pictures already sent.
  It runs in `<persistentDataPath>/Agent`. Other providers (an API with its own tool loop) would implement
  `IAgentBackend` over the same `IToolHost`.
- **Conversation** (`AgentConversation`, states Idle / Working / Reviewing): a request opens an `EditDraft`
  and puts the editor briefing in front of the message; the build appears live; then the user **accepts** it
  (one undo step named after the request), **retries** (discard and ask again; the agent is told the first try
  was turned down) or **discards** it, or sends a follow-up that refines the same draft. Stop keeps what was
  built for review. A request that changed nothing needs no review. A message may carry pictures (`AgentImage`:
  PNG, JPEG, GIF or WebP up to 3.75 MB, kept on the user's `ChatEntry`), or be pictures alone; the memory is told
  "(N pictures attached)" since it keeps words only.
- **Memory** (`IAgentMemory`, `Agent/Memory`): one long-term memory for the agent, kept in Semantix as a memory
  agent on the user's account (`SemantixMemory`, named in `AgentSettings`, "Forge"; found by name, made on first
  use, the same agent read and written). It uses the doors Semantix's own Claude Code tasks use
  (`semantix-tasksd` `claude_memory.rs`):
  - **Dress**, before every turn: `POST /api/v1/memory/agents/{id}/dress`, cued by the message (not the briefing).
    The server composes the block (standing orders, a note on how the memory works, the recalled memories) and it
    goes in front of the `<editor>` briefing. It rides in the message, not the system prompt, so a resumed
    conversation keeps its prompt cache. A memory that can't be reached never stops a turn: it runs without, and the
    chat says so once.
  - **Sleep**, once a request settles (accepted, discarded, or an answer with nothing to review): every turn
    (`MemoryCall`: the prompt without the briefing, what the agent said, a line per tool call) plus the user's
    verdict ("I accepted what you built (3 added)…"), in two halves around the model: `/sleep/prompt` (the server
    writes the distill's words), the user's own Claude Code (`ClaudeCodeMemoryModel`: one isolated `--print` answer
    on the agent's model, `--system-prompt-file`, no tools, MCP, settings or session), then `/sleep/reply` (the
    server keeps the memories). It runs in the background; the chat says "Remembered 2 things." or why it failed.
    A request still under review when the editor closes is not slept.
  - The login is the Semantix app's (`SemantixCredentials`: the bearer token in `~/.semantix/USER/credentials.json`,
    read per request). The server is `AgentSettings`' (production), unless `SEMANTIX_API_URL` is set. `SemantixHttp`
    is the only HTTP; tests fake `ISemantixHttp` and `IMemoryModel`. Claude Code's own auto memory is off in every
    process Forge starts (`CLAUDE_CODE_DISABLE_AUTO_MEMORY`), so the agent has exactly one memory.

## Runtime editor (Forge.EditorApp)

- `EditorAppBootstrap` is the composition root. It opens a **`WorldProfile`** (ScriptableObject in
  `Settings/Worlds`): the base plugin folders under StreamingAssets and the active plugin id.
  `DefaultWorld` = `Plugins` + `forge.user`. `StressWorld` = `Plugins` + `StressWorld` +
  `forge.stress_edits`. Each world has its own edit plugin, because a plugin may only override its
  masters' records. A missing base folder fails loudly instead of opening a smaller world.
  It then wires `EditSession` → `WorldView` + `WorldStreamer` → tools and UI through a shared `EditorContext`.
  It also builds the **`WaterView`** (`_water` = `Materials/Water.mat`): the active space's `WaterMap` plus its scene
  surfaces, synced after every space switch and every world change that touches a space, a lake or the terrain (so
  undo, sculpting and the agent move the water too). The sea (`World/SeaSurface`) is one quad twice the camera's far
  clip across, kept under the camera; each lake (`World/LakeSurfaces`) is a mesh of one quad per `CellRuns` run at its
  level, plus a preview surface the Water tool shows in place of a lake while its level is set. The land hides water
  where it stands higher; there are no colliders.
  And the **`SkyView`** (`World/Sky`, from `_sky`, a `SkyProfile` in `Settings/Sky`, driving the scene's directional light
  `_sun`): `Show(SkyState)` sets a runtime copy of the `Forge/Sky` skybox (horizon-to-zenith gradient, the horizon (fog)
  color below the horizon so no band shows past the land's far edge from high up, sun disk, glow and horizon haze; up to `SkyView.MaxMoons` (4) moons, each face lit from the sun's real
  direction so its crescent faces the sun, with grey markings, a faint dark side that hides the stars and a halo as
  strong as its lit share, dimmed by day; two layers of procedural stars on a grid turned by the stars' turn, fading in
  as the sun sinks from 3° to 12° below the horizon), the sky's light, exponential-squared fog in the horizon color
  that is 95% thick at the load radius (so land and sea fade into the sky), ambient light from the skybox
  (`DynamicGI.UpdateEnvironment`) and a realtime reflection probe that sees only the sky (what the water reflects).
  **The sky's light** is the one directional light: the sun's while it outshines the brightest moon, then that moon's
  (the profile's cool moonlight, by the moon's height times its lit share), swapped at dusk and dawn when both are
  faint, so there is one shadowed light, as in Skyrim; the water's glint follows it. Every day color comes from the
  profile's gradients **over the sun's height** (10° below the horizon to overhead), so dawn, noon and dusk are one
  table; the disk, glow and haze fade out over the 8° of twilight below the horizon; the moon's color, its daytime
  dimming, the moonlight and the stars' brightness are the profile's Night settings. **`SkyDirector`** (EditorApp)
  keeps it on the clock: each frame it advances `EditorContext.Clock` (if running) and re-renders
  `EditorContext.Sky` only once half a game minute has passed, the day changed or the active climate did (another
  space, a climate picked, undone or overridden; every few seconds at 20×, live while the time slider is dragged or
  the day is typed). The TIME OF DAY section shows the season, the year and today's sunrise and sunset, and each
  moon's phase, under the day. Interiors show the default sky until they get lighting templates.
  `Water.shader` (URP, transparent) colors by the water's depth from the camera depth texture, ripples the normals
  with four scrolling sine waves, reflects the sky by fresnel, adds the sun's glint and a foam line at the shore.
- **Assets**: records store engine-agnostic asset keys; `IAssetCatalog` turns a key into a scene object.
  `PrefabAssetCatalog` looks the key up in the `PrefabCatalog` ScriptableObject (`Content/PrefabCatalog.asset`,
  assigned on `EditorAppBootstrap._prefabs`) and falls back to `PrimitiveAssetCatalog` (`primitive/*`, and a
  magenta cube for unknown keys). Swapping a prefab in the catalog re-skins every object using that key
  without touching a plugin. Prefab pivots sit at the ground contact point. The content tables (prefabs, ground
  layers, materials) are each a `KeyedAssetCatalog<T>`: entries sorted by key, looked up once into a dictionary.
- **Ground looks**: a `GroundLayerRecord`'s asset key is looked up in the `GroundLayerCatalog`
  (`Content/GroundLayerCatalog.asset`, on `EditorAppBootstrap._groundLayers`, optional: without it the land is drawn
  unpainted) to a Unity `TerrainLayer` (`GroundLooks`, which also falls back to the catalog's base layer, with one
  warning, for a layer the world or catalog lacks). `TerrainChunkBuilder` gives each chunk one terrain layer per
  ground layer it names and its weights as the alphamap (64 per edge); an unpainted chunk is the base layer (grass)
  alone, and an update touches only heights or paint, whichever changed. The textures are CC0 scans from ambientCG
  under `Content/Ground/<Layer>/` (credited in `CREDITS.md`, ~8 MB a layer with its packed mask). *Forge → Ground →
  Build Ground Layers* makes each folder's `TerrainLayer` (3 m tiles), packs its mask map (metallic 0, occlusion,
  height, smoothness = 1 − roughness) and lists it under `ground/<folder>`; drop a new folder in and rebuild to add
  a layer. `Materials/Terrain.mat` (URP Terrain Lit) has `_NORMALMAP`, `_MASKMAP` and height-based blending on:
  Unity sets those keywords from the Inspector only, never for a runtime terrain.
- **Material looks**: a `MaterialRecord`'s asset key is looked up in the `MaterialCatalog`
  (`Content/MaterialCatalog.asset`, on `EditorAppBootstrap._materials`, optional: without it every object wears its own
  materials) to a Unity material (`MaterialLooks`; a material the world or catalog lacks leaves the slot in its base
  look, with one warning). `WorldView` puts a `MaterialSlots` on every placed object it builds, capturing what each
  renderer wears by default, so `Dress(overrides)` swaps `sharedMaterials` per slot and gives slots back their defaults;
  a record whose materials changed is dressed again in place, and a changed `MaterialRecord` (like a base object) is a
  dependency, so it rebuilds. The textures are CC0 ambientCG scans under `Content/Materials/<Name>/` (credited in
  `CREDITS.md`, plain git like the ground's); *Forge → Materials → Build Materials* makes each folder's URP Lit
  `<Name>.mat` (color, normal, and one packed mask as metallic-smoothness and occlusion map, `TextureMaps.PackMask`,
  shared with the ground builder) and lists it under `material/<folder>`. `MaterialCatalogTests` guards that every
  shipped material has a look with its maps on. The placeholder shapes are Unity primitives with 0–1 UVs per face, so
  a material stretches over a long wall; real meshes bring proper UVs.
- **Placeholder content** (`UnityEditor/Content`): `PlaceholderContent.All` is the one table of the base
  game's placeable objects (record name, display name, category, asset key, how to build the prefab, and the item part
  of the first items: a healing potion, a red apple and gold, filed under `Items/…`, so the assets browser gives them
  their own tab). A part whose primitive collider has the wrong shape (a coin's cylinder gets a ball) is left hollow
  and a `Box` covers it.
  *Forge → Generate Placeholder Content* builds the materials and prefabs from primitives in a preview scene
  and registers them in the catalog; *Forge → Generate Core Plugin* turns the same table into base-object
  records. Regenerating keeps asset GUIDs and each prefab's root id (so references survive) but rewrites the
  ids of the parts inside a prefab, so it shows a diff. `PrefabCatalogTests` fails if a shipped base object
  has no prefab or the catalog lists unused keys.
- **Models** (`Content/Models/<Name>/`, the real art; credited in its `CREDITS.md`, plain git like the characters):
  `<Name>.fbx` in meters, and in the art repo's naming, each optional, `<Name>_BaseColor.png`,
  `<Name>_MetallicSmoothness.png` (metal in R, smoothness in A) and `<Name>_Normal.png` (OpenGL). `ModelImport` (an
  `AssetPostprocessor`) imports every model there with no materials, cameras, lights or animation; meshes named
  `…_LOD0/1/2` are its levels of detail (Unity groups them; `ModelImport.LodHeight` gives full detail down to 15% of the
  screen's height and culls below 1%, the levels between stepping down evenly) and meshes named `…_COL` become convex
  colliders that are never drawn. *Forge → Models → Build Models* (`ModelBuilder`) makes each folder's URP Lit
  material (2048 px textures) and prefab: the model as it rests in the world, turned as its optional `<Name>.json` says
  (`{ "rest": [90, 0, 0] }`: the sword, modelled point up as a hand holds it, lies flat with its point to the front),
  its lowest point on the pivot and its footprint centred on it (the asset contract, whatever the file's pivot), the
  model inside keeping its own pivot (the sword's grip) for whatever holds it later. It lists the prefab in the prefab
  catalog under `models/<name in snake case>` (`ModelImport.KeyFor`); `CoreModels` is forge.core's records for them.
  The first is the **Viking Sword** (from `blender-tests`, an item: weapon, 9, worth 75). `ModelContentTests` fails if
  a model isn't built or breaks the contract (resting on its pivot, centred, colliding only through undrawn convex hulls).
- `EditorContext.Palette` is an engine-free `ObjectPalette` (`Forge.Editing.Palette`), cached per
  world version. UI and shortcuts read it every frame, and scanning 25k records per frame would cost
  real time. Order: category (uncategorized last), then display name, then id; `Groups` holds the
  same order per category, and 1–9 pick the first nine objects in it. `Filter(query)` keeps objects
  matching every search term in name, category or id.
- `WorldView.Sync(store)` diffs by record reference (records are immutable): unchanged →
  skipped, record changed → **updated in place** (transform / terrain heights), dependency
  (e.g. base object) changed → rebuilt. `RecordLink` maps scene objects back to record ids.
  `PreviewTerrain`/`ResetTerrain` and `ResetTransforms` support tool previews.
- **Tools** (`Tools/IEditorTool`), Photoshop style: exactly one drives the mouse. **`ToolCatalog`** is the one
  table of tools (`ToolDefinition`: kind, name, key, strip group, hint, and `EditsWorld`); the tool strip, the
  keys, the options bar and the status hint all read it. Q Select, W Move, E Rotate, R Scale, H Hand, P Place,
  O Outline, G Fill, B Sculpt, L Water, M Mark. `EditorContext.SelectTool` picks one (`ActiveTool`); tools that change the world refuse
  while the agent's build is open, and Place needs a palette object. **Holding Space lends the hand** to any
  tool (`EditorContext.HeldTool`; it only starts between drags and ends once Space and the button are let go);
  `CurrentTool` is the held tool, else the picked one. `EditorController` handles the global shortcuts, reports
  the tool's hint on every switch, and passes a filtered `ToolInput` (ray, pointer position and delta, buttons,
  modifiers) to the current tool. Clicks over the UI panel and while the right mouse button is held are removed.
  - `SelectTransformTool` serves Select, Move, Rotate and Scale (one instance each, built with its `ToolKind`
    and that tool's gizmo): click / Shift+click to select, Del deletes, Esc cancels a drag or clears the
    selection. **Box select**: a press on empty space that is dragged past the dead zone draws a box
    (`EditorContext.SelectionBox`, an engine-free `ScreenBox` in screen pixels, drawn by `SelectionBoxView`);
    letting go selects every loaded object (`WorldView.PlacedObjects`) whose renderer-bounds center shows inside
    it, Shift adds them. A press on empty space that isn't dragged clears the selection, as a click always did. **Move** drags objects themselves: pressing an object selects it (a selection it belongs to is
    kept, so the group moves together) and dragging slides them over the ground (`GroundSlide`, engine-free:
    the pointer moves across the level plane through the grabbed point, and each object is re-seated by
    `ObjectSeating` exactly as `adjust_objects` does, keeping heading and size; without ground it moves
    level). A 4-pixel dead zone keeps a selecting click from nudging anything. Its gizmo's arrows move along an
    axis, the center ball across the level plane. Rotate: X/Y/Z rings. Scale: axis cubes or center cube
    (camera-facing plane). Ctrl snaps. Every drag previews through `WorldView.PreviewObject` and commits one
    `ObjectEdits.Transform` on release; Esc resets the preview.
  - `HandTool`: left-drag pans through `ICameraRig.BeginPan` / `Pan`, the same pan as the middle button.
  - `RectangleTool` serves **Outline (O)** and **Fill (G)**, one instance per `RectPattern`: drag a rectangle from
    corner to corner to lay the palette's object around it (walls, fences) or over it (floor tiles). It uses the engine-free **`RectLayout`**
    (`Forge.Editing.Placement`), shared with the `place_outline` command: an outline is a **pinwheel** (each side
    starts at its own corner, one thickness inside the edge, and stops one thickness short of the next corner,
    so every corner is closed by exactly one piece with no overlap); a fill is rows over the whole rectangle. A
    side that isn't whole pieces ends with its last piece slid back to the corner. "Whole pieces" (default on,
    `RectangleSnapToPieces`) sizes the rectangle so it never has to: whole pieces per side plus one thickness for an
    outline, whole tiles for a fill, so walls dragged from a floor's corner meet it exactly (under the near walls,
    up to the far walls' inner face). Shift makes it square, `[` / `]` turn it (the placement turn), the gap is
    the object's row gap. Pieces are seated by `ObjectSeating` and preview as ghosts; release places them as one
    undo step and selects them. Tools that lay the palette's object (`ToolDefinition.LaysPaletteObject`: Place,
    Outline, Fill) keep going when another object is picked in the palette, and share the placement options.
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
    are remembered per base object (and kept between sessions): built things start at 0, natural things at 1 m + up to 2 m.
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
  - **Terrain brushes** share `BrushStrokeTool`: hold left mouse on the terrain; dabs fire at a fixed 30/s
    (independent of frame rate, capped per frame) and preview live, the stroke commits on release as one undo step,
    Esc cancels it. One brush (`BrushShape`: radius, strength, smooth falloff; `EditorContext.BrushRadius` /
    `BrushStrength`, the same sliders through `BrushSliders`) serves both; `[` / `]` change the radius in any tool
    whose `ToolDefinition.UsesBrush`. `BrushCursor` draws a ring that follows the ground.
  - `SculptTool` (B): `SculptStroke` into a working copy of the heights; the ring is colored by mode.
  - `PaintTool` (T, gold ring): `PaintStroke` (engine-free) moves each texel under the brush toward the chosen ground
    layer (`EditorContext.PaintLayer`, picked on the assets browser's Ground tab, named in the options bar) by the brush's weight,
    the other layers giving up their share in proportion, across chunk edges, on land only; previewed with
    `WorldView.PreviewPaint`, committed with `TerrainEdits.Commit(session, PaintStroke)` (heights untouched).
    Unpainted ground counts as the ground layer with the catalog's base look (`EditorContext.UnpaintedGround`) once
    painted beside another. Painting that layer is how ground is cleaned up. **Auto** (`EditorCommands.AutoPaintSpace`
    → `AutoPaint`, engine-free) repaints the whole space by its shape, one undo step: each texel's height and slope come
    from its chunk's own samples (fast: 729 chunks in 0.7 s), and `AutoPaintRules` give steep ground (from 34°) rock,
    slopes (from 20°) dirt, ground under the sea or up to 1.5 m above it sand, and the rest unpainted ground's layer,
    every change blended. Lakes don't make a shore yet.
  - `WaterTool` (L), a paint bucket for lakes: a press in a hollow surveys it and previews a full lake; dragging up
    or down sets the level (50 px per meter, Ctrl half meters), clamped between just above the ground and the brim,
    so it can't spill; release places it (`WaterEdits`, one undo step). A press on a lake (`WaterMap.LakeAt`, or near
    the point of one that spills) re-levels it the same way. The lake last made or pressed is `EditorContext.CurrentLake`:
    the options bar shows its level and Remove, and Del removes it (`EditorCommands.RemoveCurrentLake`).
- **The agent in the editor**: `EditorController` builds an `AgentHost` when the bootstrap has `AgentSettings`
  (`Settings/Agent/AgentSettings.asset`: Claude Code command, model `sonnet`, effort, the instructions as a
  text asset, `WorldBuilderInstructions.md`, and the memory: on, its Semantix server and its name). The host registers the building commands over the controller's
  `ObjectSeating` and the context's `PlacementOptions`, pumps the agent's main-thread queue every frame, selects
  the draft's objects (so they stand out and F frames them), and keeps the app running in the background while
  the agent works. `EditorBriefing` tells the agent where the camera looks (the default site), the terrain's
  extent and the selection. **While a draft is open, editing rests**: the controller deactivates the current tool
  if it changes the world (`ToolDefinition.EditsWorld`; Select, Hand and Mark keep working, and so do the camera
  and F), the settings card's pages, the options bar and the strip's editing tools are disabled, and `EditorContext`
  refuses edits, undo, redo and saving (`IsDraftOpen`). The agent's chat (`AgentSection`) is the **Agent** page of the
  **side dock** on the right (`SideDockSection`, a `ForgeSideDock` with an Agent and a Materials page). The dock starts
  closed; a `DockPageToggle` button opens it on its page, turns to it from another page, or closes it
  (`ForgeSideDock.Toggle`): the top bar's agent button beside the gear (pulsing while the agent works) and the assets
  browser's palette button beside the search (Materials). Without an agent its page is removed (`RemovePage`),
  and the assets browser's room stops short of the dock while it is open (the layout root wears `is-side-dock-open`). The top bar is centred, at most `--forge-toolbar-max-width` (1000 px) wide, and so is the assets browser: it stands centred in its room (`forge-assets-room`, the middle 85% of the view like the top bar's cap, click-through), so the two line up. With the dock open the room is 360 px in from both sides: still centred under the top bar, narrower on small windows. It shows the transcript, Accept / Retry /
  Discard while reviewing, and a message box (Enter sends, Shift+Enter: new line). The agent's replies are
  Markdown: `Forge.Core.Text.Markdown` (engine-free) parses the subset chat uses into blocks and styled spans, and
  `MarkdownView` lays them out (a label per block, list items as marker + text, rich text wrapped in `<noparse>`
  so the words are never read as tags). Everything else in the transcript is shown exactly as written.
  Pictures go with the next message: the camera button beside Send attaches what the view shows
  (`EditorController.SnapshotView` → `AgentPictures.Snapshot`: the camera rendered into a texture, so the panel isn't
  in it) and Ctrl+V in the message box pastes one from the clipboard (`IImageClipboard`; with words on the clipboard
  too, Ctrl+V pastes the words). They wait as thumbnails above the box (× removes one; six at most) and stay on the
  user's line in the transcript. `AgentPictures` shrinks every picture to `AgentImage.Fit` (1568 px long edge) and
  encodes PNG, or JPEG when the PNG is too heavy. `WindowsImageClipboard` reads the clipboard's "PNG" entry, else its
  bitmap (CF_DIBV5 / CF_DIB, decoded by the engine-free `Forge.Core.Imaging.DeviceIndependentBitmap`).
- **Editing and playing** (`Forge.Editing.Modes`, engine-free, tested): the app is in one `AppMode` at a time, held by
  `ModeSwitch` (`EditorContext.Modes`). The top bar's play button (Lucide `play`, beside the agent's) plays the world
  from here (`EditorContext.Play`), Esc goes back (`StopPlaying`). **Playing never writes the world**: it freezes the
  edit session (see Freezing), so not even a stray command can reach a plugin, and the context refuses edits, undo,
  redo, saving, switching space and the editing tools with "… waits: you are playing". Playing waits while the agent's
  draft is open. While playing, the controller rests every tool and shortcut (the selection is kept but doesn't glow,
  and the mark hides), and the panel's root wears `is-playing` (`PlaySection`), which hides every
  `forge-editor-chrome` element (tool strip, top bar, assets browser, side dock, status bar) and shows the play bar
  instead. The camera returns exactly to where editing left it (`EditorAppBootstrap`), and that pose is what is kept
  between sessions, even when the editor closes during play. The time of day keeps running. What plays is the player
  (`PlayFromHere`, see The player); without a player profile the editor camera flies in play instead.
- **The mark** (`GroundMark`, M / the dock's Mark button, `MarkTool`): a spot (click) or an axis-aligned area (drag)
  the user puts on the ground to show the agent where "here" is. It is editor state on `EditorContext.Mark`
  (never saved, never undone), drawn by `MarkView` in every tool, and the mark tool works while a draft is open
  because it edits nothing. `EditorBriefing` tells the agent `GroundMark.Describe()`; the camera point is the
  fallback. Lines laid on the ground (the brush ring, the mark) share `LineGizmo` + `TerrainSurface`.
- `EditorCommands` holds one-shot actions shared by shortcuts and UI buttons.
  `DropSelectionToGround` (End) finds the terrain under each selected object, looking through
  other objects, and moves the object so the lowest point of its renderer bounds rests on
  the surface. The whole selection is one undo step. `FrameSelection` (F) frames the selection.
- `ScenePicker` owns all raycasts, in world coordinates (it converts through the `WorldFrame`, see Streaming:
  origin shifting). Gizmo handles live on the **`ForgeGizmo`** layer
  (TagManager), so world picks and handle picks never interfere.
- `Gizmo` is the shared gizmo machinery: primitive parts tagged with their `GizmoHandle`, hover
  and axis tint through property blocks, and a constant on-screen size. `GizmoShapes` describes the
  translate, rotate (rings of cylinder segments) and scale shapes. Parts use the
  `GizmoOverlay` material (`Shaders/GizmoOverlay.shader`, ZTest Always, unlit), so terrain never hides
  a handle. The material is assigned on `EditorController`. `SelectionHighlighter` draws selected objects again
  each frame with `Shaders/SelectionOverlay.shader` (`Forge/SelectionOverlay`, on `EditorAppBootstrap._selectionOverlay`:
  transparent, a faint fill plus a view-angle rim, pulled slightly toward the camera), one `Graphics.RenderMesh` per
  part (`MaterialSlots.Parts`), so the objects' own materials show through; while a select tool is current, the parts
  of the slot a material ball would dress get a stronger glow. (A tint replacing the base color read on flat
  placeholder colors but vanished on textured materials.)
- **UI** (`EditorApp/UI`, UI Toolkit): the layout lives in `UI/EditorPanel.uxml`, rendered by a `UIDocument` with
  `ForgePanelSettings`. **The look is decided in one place**: `UI/Theme/ForgeTokens.uss` holds the design tokens
  (USS variables: panel glass `rgba(0,0,0,0.9)`, the warm accent, text tones, radii, spacing, type scale, motion);
  `UI/Theme/ForgeControls.uss` restyles Unity's built-in controls (buttons, fields, sliders, toggles, dropdowns and
  their menus, foldouts, scrollers, tabs) with those tokens; `UI/Theme/ForgeIcons.uss` maps icon names to the
  Lucide SVGs in `UI/Icons` (imported as `VectorImage` by the built-in vector graphics module, stroked white so
  `-unity-background-image-tint-color` colours them; `PanelElements.Icon(name)` makes one). The theme
  `ForgeRuntimeTheme.tss` imports Unity's default, then those three. Layout and Forge's own elements are in
  `UI/EditorPanel.uss` and `UI/AssetsBrowser.uss`, which only read tokens. The font is Inter (`UI/Fonts`, OFL):
  Regular by default, Medium for small caps headers, SemiBold for titles, as real cuts, never synthesized bold.
  **Components** (`EditorApp/UI/Controls`, one C# `[UxmlElement]` class and one USS sheet each, used from UXML as
  `<forge:…>`): `ForgePanel` (the glass card, optional title bar), `ForgePopup` (a large `ForgePanel` card centred over
  the editor, sized by tokens, its content scrolling; `Open`/`Close`/`Toggle(opener)`, closed by a press elsewhere, and
  by the host on a world click or Esc via `ForgePopup.CloseAll`; place it at the document root; the editor's
  settings card is one, with Space (the default) / Time / Camera / Performance / General tabs run by
  `SettingsSection`, and the top bar's gear opens it; the space, time, fly speed, world and performance sections
  live in its pages, there is no left dock), `ForgeSection` (small-caps header over content,
  collapsible with a chevron), the fields `ForgeTextInput`, `ForgeTextArea`, `ForgeNumberField` and
  `ForgeIntegerField` (delayed commit), `ForgeSlider` (number box on), `ForgeCheckbox`, `ForgeDropdown` (thin
  subclasses of Unity's controls that add the field chrome and a `unit` label, `FieldChrome`), `ForgeButton`
  (`icon`, `variant` default / primary / ghost, `small` for toolbars, `busy` to pulse while something works: it flips
  a class every `--forge-motion-pulse` and the sheet fades its opacity, UI Toolkit having no keyframes; the agent
  button is busy while the agent works; the icon is the button's background image, because a button with a
  child is no longer measured by its text), `ForgeSeparator` (a hairline, `vertical` between the items of a row),
  `ForgeSideDock` (a glass card along a side: a row of small icon tabs, as in the top bar, with a vertical separator
  between them, over its `ForgeDockPage` children, each a `key`, an `icon` and a `title` (the tooltip), one shown at a
  time; `Show(key)`, `Toggle(key)` for a page's button, `SetOpen`, `OpenChanged` / `PageChanged`;
  where it stands is the layout's classes), `TabBar`, `AssetTile`, and `Icons.Make(name)`. The theme imports
  every component sheet, so they work in any panel. Runtime UI Toolkit shows no tooltips, so `TooltipLayer` at a
  panel's root draws its own: any element's `tooltip` (UXML or code) shows beside it after a short rest, rich text
  allowed. **The UI gallery** (Forge → UI Gallery, `Scenes/UiGallery.unity`, `UI/Gallery/UiGallery.uxml`,
  `UiGallery` MonoBehaviour) shows every component in every state; judge the look there, not by hunting for a
  slider in the live editor. USS type selectors name the C# type, so rules meant for every button use
  `.unity-button`, not `Button`.
  `EditorPanel` (MonoBehaviour) only composes **one presenter per section**
  (`IPanelSection`: World, Space (with the worldspace's **Sea level**: a toggle, new seas start 2 m below the base
  land, and a number field; each change is one undo step), ToolStrip, ToolOptionsBar, Selection, Placement, Sculpt, AssetsBrowser, History,
  Performance, StatusBar, Agent). The **tool strip** (down the left edge) is an icon button per tool from
  `ToolCatalog` (`ToolDefinition.Icon`), grouped, the current tool on the accent, name and key as the tooltip.
  The **assets browser** (along the bottom, `AssetsBrowser`) is where placeable content lives: a `TabBar` with
  one tab per top-level category (`PaletteTabs` folds `ObjectPalette.Groups` by `CategoryPath.Head`), one flowing
  grid of `AssetTile`s for the current tab in display order (the sub-category is in the tooltip), a search that spans
  every tab (tabs dim, matches show with full paths, Esc clears), the 1–9 key on the first nine tiles, a collapse
  button and a drag handle on the top edge for its height. After the categories comes a **Ground** tab (key
  `/ground`, which no category head can be): one tile per `GroundLayerRecord`, its picture a material ball (`ThumbnailRenderer.GetBall`:
  a lit sphere wearing `GroundLooks.CreatePreviewMaterial`, a URP Lit stand-in built by `TerrainLayerPreview` from
  the layer's color, normal and mask maps and tint, destroyed once the picture is taken); picking one calls `EditorContext.SelectPaintLayer` (paint tool, that ground).
  The side dock's **Inspector** page (`UI/Inspector/InspectorPage`, Lucide `info` tab) shows the selection as a stack
  of sections (`IInspectorSection`: title, content built once, `Shows(Inspection)`, `Show(Inspection)` on change,
  cheap `Refresh()` per frame), each in a collapsible `ForgeSection`, in the order `EditorPanel` gives them; a new kind
  of thing in the game adds its own section and the page never changes. What is inspected is read once into an
  `Inspection` (`Forge.Editing.Inspection`, engine-free, tested: the selected records of one world version, `All<T>`,
  `Find<T>`, and `Inspection.Shared(records, read, same)` for a value they agree on or mixed) when the selection or
  the world changes, and only while the page is on show. Sections today: `IdentitySection` (name, kind, category, id,
  source plugin; several: how many of which kinds; any other record: its type and id), `TransformSection` (X, Y, Z,
  Heading, Scale as `ForgeNumberField` rows in `ForgePropertyRow`s; shared values show, differing ones show mixed,
  typing gives every selected object the value as one undo step via `EditorCommands.TransformSelection`;
  `PlacementMath.WithHeading` turns while keeping a slope tilt; a box being typed in is never overwritten) and
  `MaterialsSection` (the same `MaterialSlotList` as the Materials page, which builds itself into any host, plus a
  button to the Materials page).
  **Materials are applied, not placed**, so they live on the side dock's **Materials** page (`MaterialsPage`, brought
  up to date only while on show). On top a **preview card** (`MaterialPreview` in a `Controls/PreviewCard`): a large
  ball (`IAssetThumbnails.GetBallPreview`, 512 px, key and rim light, `MaterialBalls.PreviewOf`) standing on a studio
  backdrop painted in code (`StudioBackdrop`: a glow behind it, dark edges, a floor shadow), named, with "On Roof" when
  the chosen part wears it. It shows the ball last clicked in the library, else what the chosen part wears ("Own
  look"); a new selection or chosen part puts that part back in focus. A live, turnable preview is parked. Under it,
  two pieces, so the first can move to an inspector later: `MaterialSlotList`, the
  selection's parts (`EditorCommands.SelectionSlots`, read from `AssetSlots`, labelled by `Materials.SlotLabel`), a
  `MaterialPartRow` each with a small ball and the name of what it wears ("Own look" for the base look), clicking one
  chooses `EditorContext.MaterialSlot` (the first slot when unset; clicking an object with a select tool sets it to the
  slot of the part clicked, `ScenePicker.TryPickPlacedObject` → `MaterialSlots.SlotOf(collider)`, so "click the roof,
  then a ball" dresses the roof), with Reset look (`ResetSelectionMaterials`) and Make variant (`VariantForm`) under
  them; and `MaterialLibrary`, one ball per `MaterialRecord` (`MaterialBalls`: `MaterialLooks.CreatePreviewMaterial`, a
  throwaway copy of the catalog's material, shared by both pieces); picking one calls `EditorCommands.DressSelection`,
  which dresses the chosen slot of every selected object that has it, as one undo step, and the ball of what the
  selection wears there is lit. Material and ground tiles also **drag into the world** (`Controls/TileDrag`, a
  `PointerManipulator`: past 8 px a copy of the tile's picture follows the pointer on the panel root; letting go off
  every panel element is a drop, and the tile's click is swallowed). While carried, `EditorContext.Drag` (`AssetDrag`)
  says what, and the highlighter lights the part a material would land on (`EditorCommands.TryPartUnderPointer`, the
  controller's pointer ray). A dropped material dresses that part of that one object (`DropMaterial`, and its slot
  becomes the chosen one); a dropped ground paints one dab of the paint brush where it lands (`DropGround`, a
  one-dab `PaintStroke`, and it becomes the paint tool's ground). Each is one undo step. Every list
  is searched alike through `SearchQuery` (all terms, any field, ignoring case). Each object tile's picture comes from `ThumbnailRenderer`
  (`World/Assets`, behind `IAssetThumbnails`): on first request it instantiates the asset key through the same
  `IAssetCatalog` the world uses, frames it whole with `ThumbnailFraming` (orthographic three-quarter view, pure
  math, tested), renders it with a private camera on the `ForgePreview` layer into a 128 px texture with a
  transparent background, and keeps it for the session. The rig brings its own light and a flat ambient and
  silences the scene's lights for that one render, so pictures look the same at any time of day; the instance is
  destroyed immediately after, because the next picture may be rendered in the same frame. The **tool options bar** (across the top of the view) shows the current
  tool's name and hint, and each tool's settings are their own section inside it, shown while that tool is
  current: `SelectionSection` (count, Frame, Drop, material Slot, Reset look) for Select/Move/Rotate/Scale, `RectangleSection` (whole pieces)
  for Outline and Fill, `PlacementSection` (turn, random turn & size, align to slope, gaps) for Place, Outline and Fill, `SculptSection` (brush) for Sculpt, `WaterSection` (the current lake's level, Remove) for Water. Each presenter
  binds its UXML elements by name (`PanelElements.Required` fails loudly on a mismatch), sends
  actions to `EditorContext` / `EditorCommands`, and mirrors state in `Refresh()` every frame,
  because shortcuts change the same state. `ChoiceRow<TEnum>` is the reusable segmented control.
  Actions shared by keys and buttons live once on `EditorContext` (`SelectTool`,
  `SelectPlacement`) or `EditorCommands`. The controller and camera only see `IInputBlocker`:
  `IsPointerOverUI` uses `panel.Pick` (the full-screen root ignores picking), and
  `IsKeyboardCaptured` is true while a text input has focus (or the field hosting one: a Slider
  with a number box keeps focus itself), so typing never fires shortcuts. A click in the world or
  Esc releases UI focus; after dragging a brush slider, shortcuts resume on the next world click or Esc.
  The assets browser's tiles are plain elements, rebuilt only when the shown objects, the query or the tab
  change; fine for hundreds of objects, and a virtualized `ListView` is the next step for thousands.
- Controls: the tool keys above (Q / W / E / R / H / P / O / G / B / T / L / M) or the tool strip; hold Space → hand; 1–9 or the
  palette → Place tool (`[` / `]` rotate, hold + drag paints a row, + Shift a straight one); Esc → Select tool / deselect; Ctrl+Z / Ctrl+Y
  (or Ctrl+Shift+Z) undo/redo; Ctrl+S save; Ctrl+A selects every loaded object, Ctrl+Shift+A deselects; Ctrl+D duplicates the selection (`ObjectEdits.Duplicate`: same kind, space, transform and materials, new ids, one undo step, set down by the caller) beside itself: the selection width plus 1 m to the camera's right (`ICameraRig.Right`), seated on the ground there (slope fit as placing, else the same height above the ground), and selects the copies; Shift or Ctrl + click or box adds to the selection; hold right mouse to look, with WASD/QE to fly (the wheel then steps the
  fly speed); otherwise the wheel dollies towards the point under the cursor; hold middle mouse to pan (the grabbed
  point stays under the cursor); F frames the selection; the top bar's play button plays the world, Esc goes back to editing.
- **Fly speed.** `FlySpeed` (engine-free, `Forge.Editing.Geometry`, owned by `EditorContext`) is a multiplier on the
  camera's base speed, one of `Steps` (×0.25 … ×20). The wheel steps it while flying; the top bar's SPEED row offers
  `Presets` ×1 ×2 ×3 ×5 (a speed between them shows as its own label), and Shift still multiplies on top. "By height"
  (`FollowsHeight`, off by default) scales it with the camera's height above the ground (`HeightFactor`: none up to
  25 m, then in proportion, at most ×12); the camera measures that height with the same depth probe straight down,
  and keeps the last ground height where none is below (past the land, or over far land, which has no collider).
- `FlyCamera` (implements `ICameraRig`; its `BeginPan` / `Pan` serve the middle button and the hand tool alike) gets the UI blocker and a depth probe (`ScenePicker.TryPickWorld`)
  from the controller: wheel and pan never start over the panel, and both scale with the depth under the
  cursor. `EditorCommands.FrameSelection` fits the selection's combined renderer bounds through
  `ICameraRig.Frame`. The fit distance is the engine-free `ViewMath.FitDistance` (narrower of the vertical
  and horizontal FOV), and the camera glides there with `SmoothDamp`.
- **Editor state between sessions** (`EditorStateKeeper`): the editor opens where the user left off. Two documents,
  both engine-free classes in `Forge.Editing.State` with every value optional (null: never set, the default stands; ids
  and names that no longer fit are skipped): **`EditorPrefs`**, the user's, the same in every world (side dock open and
  its page, assets browser collapsed and height, settings card open and its tab, fly speed and by height, brush radius,
  strength and mode, objects follow terrain, random turn & size, align to slope, whole pieces), in
  `<persistentDataPath>/Editor/prefs.json`; and
  **`WorldViewState`**, per world (the space on show, the camera's pose per space in world coordinates, the tool, the
  assets tab and search, the object being placed, the time of day, the row gaps per object), in
  `Editor/Worlds/<edit plugin id>.json`. Editor state never goes into a plugin: plugins are shared data, this is one
  user's view of it (the Creation Kit keeps its prefs in an ini, not the .esp). Each file is a `JsonDocumentFile<T>`
  (`Forge.Data.Json`): camelCase, settable properties only, nulls and unknown fields skipped, written atomically
  (`AtomicFile`, shared with `PluginFileStore`) and only when its text changed; a missing or unreadable file is the
  default document, with a warning, so a lost preference never stops the editor from opening. The bootstrap reads both
  at startup (cameras and space before the first `ShowSpace`, `EditorContext.Restore`, then `EditorPanel.Restore` for
  every `IKeptSection`: `SideDockSection`, `SettingsSection`, `AssetsBrowser`), captures them once a second
  (`EditorContext.Capture`, `EditorPanel.Capture`, the cameras) and once more on `OnDestroy`. The mark, the selection
  and the agent's draft are not kept. JSON is enough for a few dozen values; if the editor comes to keep growing things
  (thumbnail caches, history across sessions), a SQLite store can replace the files behind the same two documents.
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
| `EditorApp/EditorCommands.cs` (`AutoPaintSpace`) | Auto paint's roles (slope, steep, shore) are the base game's ground looks by asset key (`ground/dirt`, `ground/rock`, `ground/sand`) | The base game's four layers are the only ground | Worlds need their own ground → the rules become data (a climate's or worldspace's `AutoPaintRules`) |
| `World/Assets/KeyedAssetCatalog.cs` | Catalog entries (prefabs, terrain layers) reference assets directly, so all content loads with the catalog at startup | A dozen placeholder prefabs and four ground layers fit in memory easily | Content outgrows memory → load prefabs by asset key through Addressables, streamed with the chunks |
| `EditorApp/EditorController.cs`, `Tools/ToolDefinition.cs` | Input read by polling devices, not an Input Actions asset; tool keys are fixed `Key`s in `ToolCatalog` | Skeleton has few fixed bindings | Rebindable controls are needed → Input Actions asset, labels read from it |
| `World/GrassView.cs` (`Grow`) | Grass patches grow on the main thread, ~10 ms for a 20k-tuft patch in the editor, so crossing into a new chunk can cost a frame | One patch a frame is a short hitch; simpler than threading while grass is new | Hitches show while flying → grow on a worker thread (`GrassScatter` is engine-free; only the buffer upload needs the main thread) |
| `EditorApp/Agent/ImageClipboard.cs` (`ForThisPlatform`) | Pasting a picture into the agent's message works on Windows only; elsewhere the clipboard has no picture | Forge runs on Windows today, and Unity's clipboard carries text only | Forge ships on macOS or Linux → a clipboard reader for that platform |
| `Agent/Memory/SemantixMemory.cs` (`Origin`) | The agent's Semantix memory is made with origin `user`, so Semantix Studio doesn't list it under Your Agents | The server knows no `forge` origin yet, and one memory for all of Forge needs no picker | Users make memories at will → a `forge` origin on the server and a Forge group in Your Agents |
