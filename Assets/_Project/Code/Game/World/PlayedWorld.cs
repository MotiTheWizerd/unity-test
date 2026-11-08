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
    /// (<see cref="WorldChanges"/>), as a saved game reads over the load order. What was taken is gone from it, what was
    /// dropped is in it, and what was moved is where it was moved to (in the block it lies in now), so whatever asks it
    /// (what is in sight, later the AI and the save) sees one world. A live view, never a copy: a change shows at once.
    /// Engine-free.
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
            if (_changes.TryGetAdded(id, out var added) || _changes.TryGetMoved(id, out added))
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
            var played = ChangedIn(space).Select(p => ObjectBlockKey.Of(p).Coord).ToArray();
            return played.Length > 0 ? chunks.Union(played).ToArray() : chunks;
        }

        public IReadOnlyList<PlacedObjectRecord> ObjectsIn(RecordId space, ChunkCoord coord)
        {
            var built = _built.ObjectsIn(space, coord);
            var key = new ObjectBlockKey(space, coord);
            var changed = ChangedIn(space).Where(p => ObjectBlockKey.Of(p) == key).ToArray();
            // Most blocks are as built: handed on as they are.
            if (changed.Length == 0 && !built.Any(p => _changes.IsRemoved(p.Id) || _changes.TryGetMoved(p.Id, out _)))
                return built;
            // What was moved is in the block it lies in now, and only there.
            var stayed = built.Where(p => !_changes.IsRemoved(p.Id) && !_changes.TryGetMoved(p.Id, out _));
            return stayed.Concat(changed).OrderBy(p => p.Id).ToArray();
        }

        // As built, less what was taken, what was moved where it is now.
        IEnumerable<T> Played<T>(IEnumerable<T> built) where T : Record
        {
            if (_changes.RemovedObjects.Count == 0 && _changes.MovedObjects.Count == 0)
                return built;
            return built
                .Where(r => !_changes.IsRemoved(r.Id))
                .Select(r => _changes.TryGetMoved(r.Id, out var moved) && moved is T now ? now : r);
        }

        // What playing put into the space, and what of the world's own it moved.
        IEnumerable<PlacedObjectRecord> ChangedIn(RecordId space) =>
            _changes.AddedObjects.Concat(_changes.MovedObjects).Where(p => p.Space == space);
    }
}
