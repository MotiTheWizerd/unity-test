using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.World.Assets;
using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// Presents a record store in the scene and keeps it in sync. Records are
    /// immutable, so a changed record is simply a different instance: Sync only
    /// touches what actually changed, updating objects in place where possible.
    /// </summary>
    public sealed class WorldView : IDisposable
    {
        readonly Transform _root;
        readonly IAssetCatalog _catalog;
        readonly Dictionary<RecordId, Presented> _presented = new Dictionary<RecordId, Presented>();
        readonly Dictionary<ChunkCoord, RecordId> _chunks = new Dictionary<ChunkCoord, RecordId>();

        public WorldView(Transform root, IAssetCatalog catalog)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int PresentedCount => _presented.Count;

        /// <summary>The scene object presenting <paramref name="id"/>, if any.</summary>
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
            if (_chunks.TryGetValue(coord, out var id) && _presented.TryGetValue(id, out var presented))
                TerrainChunkBuilder.ApplyHeights(presented.GameObject, heights);
        }

        /// <summary>Restores chunks to their record heights, discarding previews.</summary>
        public void ResetTerrain(IEnumerable<ChunkCoord> coords)
        {
            foreach (var coord in coords ?? Enumerable.Empty<ChunkCoord>())
            {
                if (_chunks.TryGetValue(coord, out var id) && _presented.TryGetValue(id, out var presented) && presented.Record is TerrainChunkRecord chunk)
                    TerrainChunkBuilder.ApplyHeights(presented.GameObject, chunk.Heights);
            }
        }

        public void Sync(IRecordStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            foreach (var id in _presented.Keys.Where(id => !store.Contains(id)).ToList())
                Remove(id);

            _chunks.Clear();
            // Ordered so duplicate-chunk resolution is deterministic.
            foreach (var record in store.All.OrderBy(r => r.Id))
            {
                switch (record)
                {
                    case TerrainChunkRecord chunk:
                        if (_chunks.TryGetValue(chunk.Coord, out var owner))
                        {
                            Debug.LogWarning($"[Forge] Terrain chunk {chunk.Coord} is defined by both '{owner}' and '{chunk.Id}'; showing '{owner}'.");
                            Remove(chunk.Id);
                            continue;
                        }

                        _chunks.Add(chunk.Coord, chunk.Id);
                        Present(chunk, null,
                            () => TerrainChunkBuilder.Build(chunk, _root),
                            go => TerrainChunkBuilder.ApplyHeights(go, chunk.Heights));
                        break;

                    case PlacedObjectRecord placed:
                        store.TryGet(placed.BaseId, out var baseRecord);
                        Present(placed, baseRecord,
                            () => BuildPlaced(placed, baseRecord as BaseObjectRecord),
                            go => ApplyTransform(go.transform, placed));
                        break;
                }
            }
        }

        public void Dispose()
        {
            foreach (var id in _presented.Keys.ToList())
                Remove(id);
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
