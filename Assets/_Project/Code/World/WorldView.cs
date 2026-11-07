using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Streaming;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// Presents the loaded chunks of one space of a record store in the scene and keeps them
    /// in sync. Which chunks are loaded is decided outside (see
    /// <see cref="WorldStreamer"/>). Records are immutable, so a changed record is
    /// simply a different instance: Sync only touches what actually changed,
    /// updating objects in place where possible.
    /// </summary>
    public sealed class WorldView : IDisposable
    {
        readonly Transform _root;
        readonly IAssetCatalog _catalog;
        readonly Material _terrainMaterial;
        readonly GroundLooks _ground;
        readonly MaterialLooks _materials;
        readonly Dictionary<RecordId, Presented> _presented = new Dictionary<RecordId, Presented>();
        readonly HashSet<ChunkCoord> _loaded = new HashSet<ChunkCoord>();
        readonly HashSet<RecordId> _hidden = new HashSet<RecordId>();
        ResolvedWorld _world;
        ChunkIndex _index = new ChunkIndex();

        /// <param name="terrainMaterial">What the land is drawn with (Materials/Terrain).</param>
        /// <param name="ground">How ground layers look; null draws the land unpainted.</param>
        /// <param name="materials">How materials look; null shows every object in its base look.</param>
        public WorldView(Transform root, IAssetCatalog catalog, Material terrainMaterial, GroundLooks ground = null, MaterialLooks materials = null)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _terrainMaterial = terrainMaterial != null ? terrainMaterial : throw new ArgumentNullException(nameof(terrainMaterial));
            _ground = ground;
            _materials = materials;
        }

        public int PresentedCount => _presented.Count;

        /// <summary>The space shown.</summary>
        public RecordId Space => _index.Space;

        /// <summary>
        /// Raised with the chunks whose terrain record changed in a <see cref="Sync"/>, loaded or not (far land is
        /// drawn from records, not from loaded chunks); after a full rebuild, with every chunk of the space.
        /// </summary>
        public event Action<IReadOnlyCollection<ChunkCoord>> TerrainChanged;

        /// <summary>The winning terrain record at <paramref name="coord"/> in the shown space, loaded or not.</summary>
        public bool TryGetTerrain(ChunkCoord coord, out TerrainChunkRecord chunk) => _index.TryGetTerrain(coord, out chunk);

        /// <summary>
        /// Shows <paramref name="space"/> instead of the current one: everything presented is removed and no chunk
        /// stays loaded, so the streamer loads the new space from scratch.
        /// </summary>
        public void ShowSpace(RecordId space)
        {
            if (space == Space)
                return;

            UnloadAll();
            _index = _world != null ? ChunkIndex.Build(_world.Records, space) : new ChunkIndex(space);
            WarnShadowed(_index.ShadowedTerrain);
        }

        /// <summary>Loaded chunk coordinates, including empty ones (they pick up content added later).</summary>
        public IReadOnlyCollection<ChunkCoord> LoadedChunks => _loaded;

        public bool IsLoaded(ChunkCoord coord) => _loaded.Contains(coord);

        /// <summary>The placed objects presented now (those in the loaded chunks of the shown space) and their scene objects.</summary>
        public IEnumerable<KeyValuePair<RecordId, GameObject>> PlacedObjects =>
            _presented
                .Where(pair => pair.Value.Record is PlacedObjectRecord)
                .Select(pair => new KeyValuePair<RecordId, GameObject>(pair.Key, pair.Value.GameObject));

        /// <summary>The scene object presenting <paramref name="id"/>, if it is loaded.</summary>
        public bool TryGetObject(RecordId id, out GameObject gameObject)
        {
            var found = _presented.TryGetValue(id, out var presented);
            gameObject = found ? presented.GameObject : null;
            return found;
        }

        /// <summary>
        /// Takes placed object <paramref name="id"/> out of the scene until <see cref="ShowHidden"/>, whether it is loaded
        /// now or streams in later, without touching its record: what playing changed (an item taken) shown over the world
        /// as built.
        /// </summary>
        public void Hide(RecordId id)
        {
            if (_hidden.Add(id))
                Remove(id);
        }

        /// <summary>Puts back everything <see cref="Hide"/> took out of the scene.</summary>
        public void ShowHidden()
        {
            if (_hidden.Count == 0)
                return;

            var chunks = new HashSet<ChunkCoord>();
            foreach (var id in _hidden)
            {
                if (_index.TryLocate(id, out var coord) && _loaded.Contains(coord))
                    chunks.Add(coord);
            }

            _hidden.Clear();
            foreach (var coord in chunks)
                PresentChunk(coord);
        }

        /// <summary>
        /// Snaps placed objects back to their record transforms, discarding any
        /// preview a tool applied directly to the scene (e.g. a cancelled drag).
        /// </summary>
        public void ResetTransforms(IEnumerable<RecordId> ids)
        {
            foreach (var id in ids ?? Enumerable.Empty<RecordId>())
            {
                if (_presented.TryGetValue(id, out var presented) && presented.Record is PlacedObjectRecord placed)
                    ApplyTransform(presented.GameObject.transform, placed);
            }
        }

        /// <summary>
        /// Shows <paramref name="preview"/>'s transform on the object presenting that record,
        /// without changing any record (e.g. during a gizmo drag). Undo it with <see cref="ResetTransforms"/>.
        /// </summary>
        public void PreviewObject(PlacedObjectRecord preview)
        {
            if (preview != null && _presented.TryGetValue(preview.Id, out var presented))
                ApplyTransform(presented.GameObject.transform, preview);
        }

        /// <summary>Shows preview heights on a chunk without changing any record (e.g. during a sculpt stroke).</summary>
        public void PreviewTerrain(ChunkCoord coord, HeightField heights)
        {
            if (TryGetPresentedTerrain(coord, out var presented))
                TerrainChunkBuilder.ApplyHeights(presented.GameObject, heights);
        }

        /// <summary>Shows preview paint on a chunk without changing any record (e.g. during a paint stroke).</summary>
        public void PreviewPaint(ChunkCoord coord, ChunkPaint paint)
        {
            if (TryGetPresentedTerrain(coord, out var presented))
                TerrainChunkBuilder.ApplyPaint(presented.GameObject, paint, _ground);
        }

        /// <summary>Restores chunks to their record heights and paint, discarding previews.</summary>
        public void ResetTerrain(IEnumerable<ChunkCoord> coords)
        {
            foreach (var coord in coords ?? Enumerable.Empty<ChunkCoord>())
            {
                if (TryGetPresentedTerrain(coord, out var presented))
                    TerrainChunkBuilder.Update(presented.GameObject, null, (TerrainChunkRecord)presented.Record, _ground);
            }
        }

        /// <summary>
        /// Switches to a new version of the world and brings the loaded chunks up to
        /// date. When the world can tell what changed since the last version (the
        /// editor's case), only the affected chunks are touched; otherwise everything is re-indexed.
        /// </summary>
        public void Sync(ResolvedWorld world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            var previous = _world;
            _world = world;
            _ground?.Use(world.Records);
            _materials?.Use(world.Records);
            if (world.TryGetChangesSince(previous, out var changed) && NoDependencyChanged(previous.Records, changed))
            {
                ApplyChanges(changed);
                // A terrain record's old and new coordinates are the same (chunks don't move), but a removed one is
                // only in the previous store and an added one only in the new.
                var terrain = new HashSet<ChunkCoord>(TerrainCoords(previous.Records, changed));
                terrain.UnionWith(TerrainCoords(world.Records, changed));
                if (terrain.Count > 0)
                    TerrainChanged?.Invoke(terrain);
            }
            else
            {
                Rebuild();
                TerrainChanged?.Invoke(_index.TerrainCoords.ToList());
            }
        }

        IEnumerable<ChunkCoord> TerrainCoords(IRecordStore store, IEnumerable<RecordId> ids)
        {
            foreach (var id in ids)
            {
                if (store.TryGet(id, out var record) && record is TerrainChunkRecord chunk && chunk.Space == Space)
                    yield return chunk.Coord;
            }
        }

        // Base objects and materials are dependencies of many placed objects; a change to one is rare (no tool edits them
        // yet) and simply falls back to a rebuild. Records the view doesn't present (spaces, lakes) are skipped by the index.
        bool NoDependencyChanged(IRecordStore previous, IEnumerable<RecordId> changed) =>
            changed.All(id => !IsDependency(previous, id) && !IsDependency(_world.Records, id));

        static bool IsDependency(IRecordStore store, RecordId id) =>
            store.TryGet(id, out var record) && (record is BaseObjectRecord || record is MaterialRecord);

        void Rebuild()
        {
            _index = ChunkIndex.Build(_world.Records, Space);
            WarnShadowed(_index.ShadowedTerrain);

            // Drop what vanished, was shadowed, or moved into a chunk that isn't loaded.
            var wanted = new HashSet<RecordId>(_loaded.SelectMany(IdsIn));
            foreach (var id in _presented.Keys.Where(id => !wanted.Contains(id)).ToList())
                Remove(id);

            foreach (var coord in _loaded)
                PresentChunk(coord);
        }

        void ApplyChanges(IReadOnlyCollection<RecordId> changed)
        {
            var dirty = _index.Update(_world.Records, changed);
            WarnShadowed(dirty.SelectMany(_index.ShadowedAt));

            foreach (var id in changed)
            {
                if (_presented.ContainsKey(id) && !(_index.TryLocate(id, out var coord) && _loaded.Contains(coord)))
                    Remove(id);
            }

            foreach (var coord in dirty.Where(_loaded.Contains))
                PresentChunk(coord);
        }

        static void WarnShadowed(IEnumerable<TerrainChunkRecord> shadowed)
        {
            foreach (var chunk in shadowed)
                Debug.LogWarning($"[Forge] Terrain chunk {chunk.Coord} of {chunk.Space} is defined by more than one plugin; '{chunk.Id}' is hidden by the record with the lowest id.");
        }

        /// <summary>Presents everything in the chunk. Returns how many records it holds (0 if already loaded).</summary>
        public int LoadChunk(ChunkCoord coord) => _loaded.Add(coord) ? PresentChunk(coord) : 0;

        public void UnloadChunk(ChunkCoord coord)
        {
            if (!_loaded.Remove(coord))
                return;

            foreach (var id in IdsIn(coord))
                Remove(id);
        }

        public void Dispose() => UnloadAll();

        void UnloadAll()
        {
            foreach (var id in _presented.Keys.ToList())
                Remove(id);
            _loaded.Clear();
        }

        IEnumerable<RecordId> IdsIn(ChunkCoord coord)
        {
            if (_index.TryGetTerrain(coord, out var chunk))
                yield return chunk.Id;
            foreach (var placed in _index.ObjectsIn(coord))
                yield return placed.Id;
        }

        int PresentChunk(ChunkCoord coord)
        {
            // A newly winning terrain record replaces the one shown so far.
            foreach (var shadowed in _index.ShadowedAt(coord))
                Remove(shadowed.Id);

            var count = 0;
            if (_index.TryGetTerrain(coord, out var chunk))
            {
                // The record shown so far, so an update touches only heights or paint, whichever changed.
                var shown = _presented.TryGetValue(chunk.Id, out var current) ? current.Record as TerrainChunkRecord : null;
                Present(chunk, null,
                    () => TerrainChunkBuilder.Build(chunk, _root, _terrainMaterial, _ground),
                    go => TerrainChunkBuilder.Update(go, shown, chunk, _ground));
                count++;
            }

            foreach (var placed in _index.ObjectsIn(coord))
            {
                if (_hidden.Contains(placed.Id))
                    continue;
                _world.Records.TryGet(placed.BaseId, out var baseRecord);
                var shown = _presented.TryGetValue(placed.Id, out var current) ? current.Record as PlacedObjectRecord : null;
                Present(placed, baseRecord,
                    () => BuildPlaced(placed, baseRecord as BaseObjectRecord),
                    go =>
                    {
                        ApplyTransform(go.transform, placed);
                        // A base object's change rebuilds the object, so only its own materials can differ here.
                        if (!placed.HasSameMaterials(shown))
                            Dress(go, placed, baseRecord as BaseObjectRecord);
                    });
                count++;
            }

            return count;
        }

        bool TryGetPresentedTerrain(ChunkCoord coord, out Presented presented)
        {
            presented = default;
            return _index.TryGetTerrain(coord, out var chunk) && _presented.TryGetValue(chunk.Id, out presented);
        }

        // Records are immutable, so reference equality tells what changed:
        // same record and dependency → nothing to do; only the record changed →
        // update the existing object in place; the dependency changed → rebuild.
        void Present(Record record, Record dependency, Func<GameObject> build, Action<GameObject> update)
        {
            if (_presented.TryGetValue(record.Id, out var current))
            {
                if (ReferenceEquals(current.Dependency, dependency))
                {
                    if (!ReferenceEquals(current.Record, record))
                    {
                        update(current.GameObject);
                        _presented[record.Id] = new Presented(record, dependency, current.GameObject);
                    }

                    return;
                }

                Remove(record.Id);
            }

            var go = build();
            go.AddComponent<RecordLink>().RecordId = record.Id;
            _presented.Add(record.Id, new Presented(record, dependency, go));
        }

        GameObject BuildPlaced(PlacedObjectRecord placed, BaseObjectRecord baseRecord)
        {
            if (baseRecord == null)
                Debug.LogWarning($"[Forge] '{placed.Id}' references missing base object '{placed.BaseId}'.");

            var go = _catalog.Instantiate(baseRecord?.AssetKey, _root);
            go.name = placed.Id.ToString();
            ApplyTransform(go.transform, placed);
            MaterialSlots.Capture(go);
            if (placed.Materials.Count > 0 || baseRecord?.Materials != null)
                Dress(go, placed, baseRecord);
            return go;
        }

        // Every slot the base object (a variant) or the placed object names wears its material, the placed object's
        // winning; the rest wear the asset's own.
        void Dress(GameObject go, PlacedObjectRecord placed, BaseObjectRecord baseRecord)
        {
            var worn = MaterialOverrides.Layer(baseRecord?.Materials, placed.Materials);
            if (_materials != null)
                _materials.Dress(go, worn);
        }

        static void ApplyTransform(Transform transform, PlacedObjectRecord placed)
        {
            transform.localPosition = placed.Position.ToUnity();
            transform.localRotation = placed.Rotation.ToUnity();
            transform.localScale = Vector3.one * placed.Scale;
        }

        void Remove(RecordId id)
        {
            if (!_presented.TryGetValue(id, out var presented))
                return;

            _presented.Remove(id);
            if (presented.Record is TerrainChunkRecord)
                TerrainChunkBuilder.Destroy(presented.GameObject);
            else
                UnityEngine.Object.Destroy(presented.GameObject);
        }

        readonly struct Presented
        {
            public Presented(Record record, Record dependency, GameObject gameObject)
            {
                Record = record;
                Dependency = dependency;
                GameObject = gameObject;
            }

            public Record Record { get; }

            public Record Dependency { get; }

            public GameObject GameObject { get; }
        }
    }
}
