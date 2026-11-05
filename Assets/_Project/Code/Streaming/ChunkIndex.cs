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
    /// The presentable records of one space by terrain chunk, so the space can be loaded and unloaded chunk by
    /// chunk. Terrain records are indexed up front (one per chunk, small); placed objects are the store's, read a
    /// chunk at a time when a chunk is asked for (<see cref="IRecordStore.ObjectsIn"/>), so building the index reads
    /// no objects. Records of other spaces are left out. Build it once, then <see cref="Update"/> it with the ids
    /// that changed; the cost of an update follows the change, not the world.
    /// </summary>
    public sealed class ChunkIndex
    {
        static readonly IReadOnlyList<PlacedObjectRecord> NoObjects = Array.Empty<PlacedObjectRecord>();
        static readonly IReadOnlyList<TerrainChunkRecord> NoTerrain = Array.Empty<TerrainChunkRecord>();
        static readonly IComparer<Record> ById = Comparer<Record>.Create((a, b) => a.Id.CompareTo(b.Id));

        // All terrain records per coordinate, sorted by id; the first one wins.
        readonly Dictionary<ChunkCoord, List<TerrainChunkRecord>> _terrain = new Dictionary<ChunkCoord, List<TerrainChunkRecord>>();

        // Where each terrain record is, and where each object served so far was last seen.
        readonly Dictionary<RecordId, ChunkCoord> _location = new Dictionary<RecordId, ChunkCoord>();
        IRecordStore _store;

        /// <param name="space">The space indexed; default for an empty index that shows nothing.</param>
        public ChunkIndex(RecordId space = default)
        {
            Space = space;
        }

        /// <summary>The space whose records are indexed.</summary>
        public RecordId Space { get; }

        public int TerrainCount => _terrain.Count;

        /// <summary>How many objects the space holds. Reads every chunk of the space.</summary>
        public int ObjectCount =>
            _store == null || Space.IsDefault ? 0 : _store.ObjectChunks(Space).Sum(coord => ObjectsIn(coord).Count);

        /// <summary>
        /// Terrain records hidden because another namespace defines the same
        /// coordinate. The lowest record id wins, so the choice is deterministic.
        /// </summary>
        public IEnumerable<TerrainChunkRecord> ShadowedTerrain =>
            _terrain.Values.SelectMany(list => list.Skip(1)).OrderBy(c => c.Id);

        public static ChunkIndex Build(IRecordStore store, RecordId space)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var index = new ChunkIndex(space) { _store = store };
            foreach (var chunk in store.OfType<TerrainChunkRecord>())
            {
                if (chunk.Space == space)
                    index.AddTerrain(chunk, sorted: false);
            }

            // Store enumeration order is unspecified; sort once so presentation is deterministic.
            foreach (var list in index._terrain.Values)
                list.Sort(ById);
            return index;
        }

        public static ChunkCoord ChunkOf(PlacedObjectRecord placed) =>
            WorldGrid.ChunkOf(placed.Position.X, placed.Position.Z);

        /// <summary>
        /// Moves the index to the new <paramref name="store"/>, re-indexes <paramref name="changed"/>
        /// and returns every chunk whose content changed: the new location of each record, and the old one when
        /// the index has served that chunk (an object's old chunk is unknown until <see cref="ObjectsIn"/> read it;
        /// a chunk nobody asked for has nothing shown to refresh). Ids of other record types or other spaces are ignored.
        /// </summary>
        public IReadOnlyCollection<ChunkCoord> Update(IRecordStore store, IEnumerable<RecordId> changed)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (changed == null)
                throw new ArgumentNullException(nameof(changed));

            _store = store;
            var dirty = new HashSet<ChunkCoord>();
            foreach (var id in changed)
            {
                if (_location.TryGetValue(id, out var oldCoord))
                {
                    Remove(id, oldCoord);
                    dirty.Add(oldCoord);
                }

                if (store.TryGet(id, out var record) && Add(record) is ChunkCoord newCoord)
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

        /// <summary>Placed objects in the chunk, sorted by id; empty if there are none. Reads the chunk on first use.</summary>
        public IReadOnlyList<PlacedObjectRecord> ObjectsIn(ChunkCoord coord)
        {
            if (_store == null || Space.IsDefault)
                return NoObjects;

            var objects = _store.ObjectsIn(Space, coord);
            foreach (var placed in objects)
                _location[placed.Id] = coord;
            return objects;
        }

        /// <summary>The chunk a record is shown in; false if it isn't in this space or is shadowed terrain.</summary>
        public bool TryLocate(RecordId id, out ChunkCoord coord)
        {
            if (_location.TryGetValue(id, out coord))
                return !_terrain.TryGetValue(coord, out var terrain) || terrain[0].Id == id || !terrain.Exists(c => c.Id == id);

            // An object in a chunk nobody asked for yet.
            if (_store != null && _store.TryGet(id, out var record) && record is PlacedObjectRecord placed && placed.Space == Space)
            {
                coord = ChunkOf(placed);
                return true;
            }

            return false;
        }

        ChunkCoord? Add(Record record)
        {
            switch (record)
            {
                case TerrainChunkRecord chunk when chunk.Space == Space:
                    AddTerrain(chunk, sorted: true);
                    return chunk.Coord;

                case PlacedObjectRecord placed when placed.Space == Space:
                    // The store already holds it in its chunk; only its location is noted here.
                    var coord = ChunkOf(placed);
                    _location[placed.Id] = coord;
                    return coord;

                default:
                    return null;
            }
        }

        void AddTerrain(TerrainChunkRecord chunk, bool sorted)
        {
            if (!_terrain.TryGetValue(chunk.Coord, out var list))
                _terrain.Add(chunk.Coord, list = new List<TerrainChunkRecord>());

            if (sorted)
            {
                var at = list.BinarySearch(chunk, ById);
                list.Insert(at < 0 ? ~at : at, chunk);
            }
            else
            {
                list.Add(chunk);
            }

            _location[chunk.Id] = chunk.Coord;
        }

        void Remove(RecordId id, ChunkCoord coord)
        {
            _location.Remove(id);
            if (!_terrain.TryGetValue(coord, out var list))
                return;

            var at = list.FindIndex(c => c.Id == id);
            if (at < 0)
                return;
            list.RemoveAt(at);
            if (list.Count == 0)
                _terrain.Remove(coord);
        }
    }
}
