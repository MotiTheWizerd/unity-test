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
    /// rebuilds what actually changed.
    /// </summary>
    public sealed class WorldView : IDisposable
    {
        readonly Transform _root;
        readonly IAssetCatalog _catalog;
        readonly Dictionary<RecordId, Presented> _presented = new Dictionary<RecordId, Presented>();

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

        public void Sync(IRecordStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            foreach (var id in _presented.Keys.Where(id => !store.Contains(id)).ToList())
                Remove(id);

            var claimedChunks = new Dictionary<ChunkCoord, RecordId>();
            // Ordered so duplicate-chunk resolution is deterministic.
            foreach (var record in store.All.OrderBy(r => r.Id))
            {
                switch (record)
                {
                    case TerrainChunkRecord chunk:
                        if (claimedChunks.TryGetValue(chunk.Coord, out var owner))
                        {
                            Debug.LogWarning($"[Forge] Terrain chunk {chunk.Coord} is defined by both '{owner}' and '{chunk.Id}'; showing '{owner}'.");
                            Remove(chunk.Id);
                            continue;
                        }

                        claimedChunks.Add(chunk.Coord, chunk.Id);
                        Present(chunk, null, () => TerrainChunkBuilder.Build(chunk, _root));
                        break;

                    case PlacedObjectRecord placed:
                        store.TryGet(placed.BaseId, out var baseRecord);
                        Present(placed, baseRecord, () => BuildPlaced(placed, baseRecord as BaseObjectRecord));
                        break;
                }
            }
        }

        public void Dispose()
        {
            foreach (var id in _presented.Keys.ToList())
                Remove(id);
        }

        // Rebuilds only when the record (or the base it depends on) is a different instance.
        void Present(Record record, Record dependency, Func<GameObject> build)
        {
            if (_presented.TryGetValue(record.Id, out var current))
            {
                if (ReferenceEquals(current.Record, record) && ReferenceEquals(current.Dependency, dependency))
                    return;
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
