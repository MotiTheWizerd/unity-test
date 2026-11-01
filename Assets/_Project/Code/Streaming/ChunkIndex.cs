using System;
using System.Collections.Generic;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Streaming
{
    /// <summary>
    /// The presentable records of a world grouped by terrain chunk, so the world
    /// can be loaded and unloaded chunk by chunk. A placed object belongs to the
    /// chunk under its position. Immutable: rebuild it when the world changes.
    /// </summary>
    public sealed class ChunkIndex
    {
        static readonly IReadOnlyList<PlacedObjectRecord> NoObjects = Array.Empty<PlacedObjectRecord>();

        readonly Dictionary<ChunkCoord, TerrainChunkRecord> _terrain;
        readonly Dictionary<ChunkCoord, List<PlacedObjectRecord>> _objects;

        ChunkIndex(
            Dictionary<ChunkCoord, TerrainChunkRecord> terrain,
            Dictionary<ChunkCoord, List<PlacedObjectRecord>> objects,
            IReadOnlyList<TerrainChunkRecord> shadowedTerrain,
            int objectCount)
        {
            _terrain = terrain;
            _objects = objects;
            ShadowedTerrain = shadowedTerrain;
            ObjectCount = objectCount;
        }

        public static ChunkIndex Empty { get; } = new ChunkIndex(
            new Dictionary<ChunkCoord, TerrainChunkRecord>(),
            new Dictionary<ChunkCoord, List<PlacedObjectRecord>>(),
            Array.Empty<TerrainChunkRecord>(),
            0);

        public int TerrainCount => _terrain.Count;

        public int ObjectCount { get; }

        /// <summary>
        /// Terrain records hidden because another namespace defines the same
        /// coordinate. The lowest record id wins, so the choice is deterministic.
        /// </summary>
        public IReadOnlyList<TerrainChunkRecord> ShadowedTerrain { get; }

        public static ChunkIndex Build(IRecordStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var terrain = new Dictionary<ChunkCoord, TerrainChunkRecord>();
            var shadowed = new List<TerrainChunkRecord>();
            foreach (var chunk in store.OfType<TerrainChunkRecord>())
            {
                if (!terrain.TryGetValue(chunk.Coord, out var current))
                {
                    terrain.Add(chunk.Coord, chunk);
                }
                else if (chunk.Id.CompareTo(current.Id) < 0)
                {
                    shadowed.Add(current);
                    terrain[chunk.Coord] = chunk;
                }
                else
                {
                    shadowed.Add(chunk);
                }
            }

            var objects = new Dictionary<ChunkCoord, List<PlacedObjectRecord>>();
            var objectCount = 0;
            foreach (var placed in store.OfType<PlacedObjectRecord>())
            {
                var coord = ChunkOf(placed);
                if (!objects.TryGetValue(coord, out var list))
                    objects.Add(coord, list = new List<PlacedObjectRecord>());
                list.Add(placed);
                objectCount++;
            }

            // Store enumeration order is unspecified; sort so presentation is deterministic.
            foreach (var list in objects.Values)
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
            shadowed.Sort((a, b) => a.Id.CompareTo(b.Id));

            return new ChunkIndex(terrain, objects, shadowed, objectCount);
        }

        public static ChunkCoord ChunkOf(PlacedObjectRecord placed) =>
            WorldGrid.ChunkOf(placed.Position.X, placed.Position.Z);

        public bool TryGetTerrain(ChunkCoord coord, out TerrainChunkRecord chunk) =>
            _terrain.TryGetValue(coord, out chunk);

        /// <summary>Placed objects in the chunk, sorted by id; empty if there are none.</summary>
        public IReadOnlyList<PlacedObjectRecord> ObjectsIn(ChunkCoord coord) =>
            _objects.TryGetValue(coord, out var list) ? list : NoObjects;
    }
}
