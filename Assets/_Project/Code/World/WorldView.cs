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
    /// Presents the loaded chunks of a record store in the scene and keeps them
    /// in sync. Which chunks are loaded is decided outside (see
    /// <see cref="WorldStreamer"/>). Records are immutable, so a changed record is
    /// simply a different instance: Sync only touches what actually changed,
    /// updating objects in place where possible.
    /// </summary>
    public sealed class WorldView : IDisposable
    {
        readonly Transform _root;
        readonly IAssetCatalog _catalog;
        readonly Dictionary<RecordId, Presented> _presented = new Dictionary<RecordId, Presented>();
        readonly HashSet<ChunkCoord> _loaded = new HashSet<ChunkCoord>();
        IRecordStore _store;
        ChunkIndex _index = ChunkIndex.Empty;

        public WorldView(Transform root, IAssetCatalog catalog)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int PresentedCount => _presented.Count;

        /// <summary>Loaded chunk coordinates, including empty ones (they pick up content added later).</summary>
        public IReadOnlyCollection<ChunkCoord> LoadedChunks => _loaded;

        public bool IsLoaded(ChunkCoord coord) => _loaded.Contains(coord);

        /// <summary>The scene object presenting <paramref name="id"/>, if it is loaded.</summary>
        public bool TryGetObject(RecordId id, out GameObject gameObject)
        {
            var found = _presented.TryGetValue(id, out var presented);
            gameObject = found ? presented.GameObject : null;
            return found;
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

        /// <summary>Shows preview heights on a chunk without changing any record (e.g. during a sculpt stroke).</summary>
        public void PreviewTerrain(ChunkCoord coord, HeightField heights)
        {
            if (TryGetPresentedTerrain(coord, out var presented))
                TerrainChunkBuilder.ApplyHeights(presented.GameObject, heights);
        }

        /// <summary>Restores chunks to their record heights, discarding previews.</summary>
        public void ResetTerrain(IEnumerable<ChunkCoord> coords)
        {
            foreach (var coord in coords ?? Enumerable.Empty<ChunkCoord>())
            {
                if (TryGetPresentedTerrain(coord, out var presented))
                    TerrainChunkBuilder.ApplyHeights(presented.GameObject, ((TerrainChunkRecord)presented.Record).Heights);
            }
        }

        /// <summary>Switches to a new version of the world and brings the loaded chunks up to date.</summary>
        public void Sync(IRecordStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            // SHORTCUT: rebuilds the whole index on every world change (~6-15 ms at
            // 25k records). Make it incremental together with EditSession.Resolve,
            // from the record ids each edit step already knows it changed.
            _index = ChunkIndex.Build(store);

            foreach (var shadowed in _index.ShadowedTerrain)
            {
                _index.TryGetTerrain(shadowed.Coord, out var owner);
                Debug.LogWarning($"[Forge] Terrain chunk {shadowed.Coord} is defined by both '{owner.Id}' and '{shadowed.Id}'; showing '{owner.Id}'.");
            }

            // Drop what vanished, was shadowed, or moved into a chunk that isn't loaded.
            var wanted = new HashSet<RecordId>(_loaded.SelectMany(IdsIn));
            foreach (var id in _presented.Keys.Where(id => !wanted.Contains(id)).ToList())
                Remove(id);

            foreach (var coord in _loaded)
                PresentChunk(coord);
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

        public void Dispose()
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
            var count = 0;
            if (_index.TryGetTerrain(coord, out var chunk))
            {
                Present(chunk, null,
                    () => TerrainChunkBuilder.Build(chunk, _root),
                    go => TerrainChunkBuilder.ApplyHeights(go, chunk.Heights));
                count++;
            }

            foreach (var placed in _index.ObjectsIn(coord))
            {
                _store.TryGet(placed.BaseId, out var baseRecord);
                Present(placed, baseRecord,
                    () => BuildPlaced(placed, baseRecord as BaseObjectRecord),
                    go => ApplyTransform(go.transform, placed));
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
            return go;
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
