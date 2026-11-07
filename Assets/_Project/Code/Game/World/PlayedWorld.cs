using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Game.World
{
    /// <summary>
    /// The world as it is being played: the world as built with what playing changed laid on top
    /// (<see cref="WorldChanges"/>), as a saved game reads over the load order. What was taken is gone from it and what
    /// was dropped is in it, so whatever asks it (what is in sight, later the AI and the save) sees one world. A live
    /// view, never a copy: a change shows at once. Engine-free.
    /// </summary>
    public sealed class PlayedWorld : IRecordStore
    {
        readonly IRecordStore _built;
        readonly WorldChanges _changes;

        public PlayedWorld(IRecordStore built, WorldChanges changes)
        {
            _built = built ?? throw new ArgumentNullException(nameof(built));
            _changes = changes ?? throw new ArgumentNullException(nameof(changes));
        }

        public int Count => _built.Count - _changes.RemovedObjects.Count(_built.Contains) + _changes.AddedObjects.Count;

        public IEnumerable<Record> All => Played(_built.All).Concat(_changes.AddedObjects);

        public bool Contains(RecordId id) => _changes.TryGetAdded(id, out _) || (!_changes.IsRemoved(id) && _built.Contains(id));

        public bool TryGet(RecordId id, out Record record)
        {
            if (_changes.TryGetAdded(id, out var added))
            {
                record = added;
                return true;
            }

            if (!_changes.IsRemoved(id))
                return _built.TryGet(id, out record);

            record = null;
            return false;
        }

        public T Get<T>(RecordId id) where T : Record => RecordLookup.Get<T>(this, id);

        public IEnumerable<T> OfType<T>() where T : Record => Played(_built.OfType<T>()).Concat(_changes.AddedObjects.OfType<T>());

        /// <summary>The plugin whose record won; for what playing added, <see cref="WorldChanges.Namespace"/>.</summary>
        public string GetSource(RecordId id)
        {
            if (_changes.TryGetAdded(id, out _))
                return WorldChanges.Namespace;
            if (_changes.IsRemoved(id))
                throw RecordLookup.Missing(id);
            return _built.GetSource(id);
        }

        public IReadOnlyList<ChunkCoord> ObjectChunks(RecordId space)
        {
            var chunks = _built.ObjectChunks(space);
            var added = AddedIn(space).Select(p => ObjectBlockKey.Of(p).Coord).ToArray();
            return added.Length > 0 ? chunks.Union(added).ToArray() : chunks;
        }

        public IReadOnlyList<PlacedObjectRecord> ObjectsIn(RecordId space, ChunkCoord coord)
        {
            var built = _built.ObjectsIn(space, coord);
            var key = new ObjectBlockKey(space, coord);
            var added = AddedIn(space).Where(p => ObjectBlockKey.Of(p) == key).ToArray();
            // Most blocks are as built: handed on as they are.
            if (added.Length == 0 && !built.Any(p => _changes.IsRemoved(p.Id)))
                return built;
            return Played(built).Concat(added).OrderBy(p => p.Id).ToArray();
        }

        IEnumerable<T> Played<T>(IEnumerable<T> built) where T : Record =>
            _changes.RemovedObjects.Count == 0 ? built : built.Where(r => !_changes.IsRemoved(r.Id));

        IEnumerable<PlacedObjectRecord> AddedIn(RecordId space) => _changes.AddedObjects.Where(p => p.Space == space);
    }
}
