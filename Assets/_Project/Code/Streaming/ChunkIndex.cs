using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Streaming
{
    /// <summary>
    /// The presentable records of a world grouped by terrain chunk, so the world
    /// can be loaded and unloaded chunk by chunk. A placed object belongs to the
    /// chunk under its position. Build it once, then <see cref="Update"/> it with
    /// the ids that changed; the cost of an update follows the change, not the world.
    /// </summary>
    public sealed class ChunkIndex
    {
        static readonly IReadOnlyList<PlacedObjectRecord> NoObjects = Array.Empty<PlacedObjectRecord>();
        static readonly IReadOnlyList<TerrainChunkRecord> NoTerrain = Array.Empty<TerrainChunkRecord>();
        static readonly IComparer<Record> ById = Comparer<Record>.Create((a, b) => a.Id.CompareTo(b.Id));

        // All terrain records per coordinate, sorted by id; the first one wins.
        readonly Dictionary<ChunkCoord, List<TerrainChunkRecord>> _terrain = new Dictionary<ChunkCoord, List<TerrainChunkRecord>>();
        readonly Dictionary<ChunkCoord, List<PlacedObjectRecord>> _objects = new Dictionary<ChunkCoord, List<PlacedObjectRecord>>();
        readonly Dictionary<RecordId, ChunkCoord> _location = new Dictionary<RecordId, ChunkCoord>();

        public int TerrainCount => _terrain.Count;

        public int ObjectCount { get; private set; }

        /// <summary>
        /// Terrain records hidden because another namespace defines the same
        /// coordinate. The lowest record id wins, so the choice is deterministic.
        /// </summary>
        public IEnumerable<TerrainChunkRecord> ShadowedTerrain =>
            _terrain.Values.SelectMany(list => list.Skip(1)).OrderBy(c => c.Id);

        public static ChunkIndex Build(IRecordStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var index = new ChunkIndex();
            foreach (var chunk in store.OfType<TerrainChunkRecord>())
                index.Add(chunk, sorted: false);
            foreach (var placed in store.OfType<PlacedObjectRecord>())
                index.Add(placed, sorted: false);

            // Store enumeration order is unspecified; sort once so presentation is deterministic.
            foreach (var list in index._terrain.Values)
                list.Sort(ById);
            foreach (var list in index._objects.Values)
                list.Sort(ById);
            return index;
        }

        public static ChunkCoord ChunkOf(PlacedObjectRecord placed) =>
            WorldGrid.ChunkOf(placed.Position.X, placed.Position.Z);

        /// <summary>
        /// Re-indexes <paramref name="changed"/> against the new <paramref name="store"/>
        /// and returns every chunk whose content changed (old and new location of each record).
        /// Ids of other record types are ignored.
        /// </summary>
        public IReadOnlyCollection<ChunkCoord> Update(IRecordStore store, IEnumerable<RecordId> changed)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (changed == null)
                throw new ArgumentNullException(nameof(changed));

            var dirty = new HashSet<ChunkCoord>();
            foreach (var id in changed)
            {
                if (_location.TryGetValue(id, out var oldCoord))
                {
                    Remove(id, oldCoord);
                    dirty.Add(oldCoord);
                }

                if (store.TryGet(id, out var record) && Add(record, sorted: true) is ChunkCoord newCoord)
                    dirty.Add(newCoord);
            }

            return dirty;
        }

        public bool TryGetTerrain(ChunkCoord coord, out TerrainChunkRecord chunk)
        {
            var found = _terrain.TryGetValue(coord, out var list);
            chunk = found ? list[0] : null;
            return found;
        }

        /// <summary>Terrain records at <paramref name="coord"/> hidden by the winning one.</summary>
        public IReadOnlyList<TerrainChunkRecord> ShadowedAt(ChunkCoord coord) =>
            _terrain.TryGetValue(coord, out var list) && list.Count > 1 ? list.GetRange(1, list.Count - 1) : NoTerrain;

        /// <summary>Placed objects in the chunk, sorted by id; empty if there are none.</summary>
        public IReadOnlyList<PlacedObjectRecord> ObjectsIn(ChunkCoord coord) =>
            _objects.TryGetValue(coord, out var list) ? list : NoObjects;

        /// <summary>The chunk a record is shown in; false if it isn't indexed or is shadowed terrain.</summary>
        public bool TryLocate(RecordId id, out ChunkCoord coord)
        {
            if (!_location.TryGetValue(id, out coord))
                return false;

            // Of the terrain records at a coordinate, only the winner is shown.
            return !_terrain.TryGetValue(coord, out var terrain) || terrain[0].Id == id || !terrain.Exists(c => c.Id == id);
        }

        ChunkCoord? Add(Record record, bool sorted)
        {
            switch (record)
            {
                case TerrainChunkRecord chunk:
                    Insert(_terrain, chunk.Coord, chunk, sorted);
                    _location[chunk.Id] = chunk.Coord;
                    return chunk.Coord;

                case PlacedObjectRecord placed:
                    var coord = ChunkOf(placed);
                    Insert(_objects, coord, placed, sorted);
                    _location[placed.Id] = coord;
                    ObjectCount++;
                    return coord;

                default:
                    return null;
            }
        }

        void Remove(RecordId id, ChunkCoord coord)
        {
            _location.Remove(id);
            if (RemoveFrom(_terrain, coord, id))
                return;
            if (RemoveFrom(_objects, coord, id))
                ObjectCount--;
        }

        static void Insert<T>(Dictionary<ChunkCoord, List<T>> map, ChunkCoord coord, T record, bool sorted) where T : Record
        {
            if (!map.TryGetValue(coord, out var list))
                map.Add(coord, list = new List<T>());

            if (!sorted)
            {
                list.Add(record);
                return;
            }

            var at = list.BinarySearch(record, ById);
            list.Insert(at < 0 ? ~at : at, record);
        }

        static bool RemoveFrom<T>(Dictionary<ChunkCoord, List<T>> map, ChunkCoord coord, RecordId id) where T : Record
        {
            if (!map.TryGetValue(coord, out var list))
                return false;

            var at = list.FindIndex(r => r.Id == id);
            if (at < 0)
                return false;

            list.RemoveAt(at);
            if (list.Count == 0)
                map.Remove(coord);
            return true;
        }
    }
}
