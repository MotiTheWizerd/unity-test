using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;

namespace Forge.Game.World
{
    /// <summary>
    /// What playing has changed in the world, kept beside it the way a saved game keeps its changes over the load order
    /// (Bethesda's changed references): the world's records stay exactly as built, and whatever shows the world while
    /// playing lays these on top (<see cref="PlayedWorld"/>). For now the placed objects taken out of it, the ones
    /// playing put in (an item dropped) and the ones it moved (a cup knocked over, a basket carried off: <see cref="Move"/>);
    /// opened doors and emptied containers will join them, and the save game will write it all. Engine-free.
    /// </summary>
    public sealed class WorldChanges
    {
        /// <summary>The namespace of the placed objects playing makes, which no plugin writes.</summary>
        public const string Namespace = "forge.play";

        readonly HashSet<RecordId> _removed = new HashSet<RecordId>();
        readonly Dictionary<RecordId, PlacedObjectRecord> _added = new Dictionary<RecordId, PlacedObjectRecord>();
        readonly Dictionary<RecordId, PlacedObjectRecord> _moved = new Dictionary<RecordId, PlacedObjectRecord>();
        int _lastId;

        /// <summary>Raised with a placed object as it is put into the world.</summary>
        public event Action<PlacedObjectRecord> Added;

        /// <summary>Raised with a placed object, built or added, as it is where it was moved to.</summary>
        public event Action<PlacedObjectRecord> Moved;

        /// <summary>Raised with a placed object as it is taken out of the world, built or added.</summary>
        public event Action<RecordId> Removed;

        /// <summary>Raised when every change is let go: the world is as built again.</summary>
        public event Action Cleared;

        /// <summary>The world's own placed objects taken out of it.</summary>
        public IReadOnlyCollection<RecordId> RemovedObjects => _removed;

        /// <summary>The placed objects playing put into the world and that are still there.</summary>
        public IReadOnlyCollection<PlacedObjectRecord> AddedObjects => _added.Values;

        /// <summary>Whether the world's own placed object <paramref name="placed"/> was taken out of it.</summary>
        public bool IsRemoved(RecordId placed) => _removed.Contains(placed);

        /// <summary>The placed object <paramref name="id"/> playing put into the world, if it is still there.</summary>
        public bool TryGetAdded(RecordId id, out PlacedObjectRecord placed) => _added.TryGetValue(id, out placed);

        /// <summary>The world's own placed objects playing moved, where they are now.</summary>
        public IReadOnlyCollection<PlacedObjectRecord> MovedObjects => _moved.Values;

        /// <summary>The world's own placed object <paramref name="id"/> where playing moved it, if it did and it is still there.</summary>
        public bool TryGetMoved(RecordId id, out PlacedObjectRecord placed) => _moved.TryGetValue(id, out placed);

        /// <summary>
        /// The placed object <paramref name="moved"/> names, built or added, is now where <paramref name="moved"/> says
        /// (only its place differs: it fell, it was carried). False, and nothing changed, when it is no longer in the world.
        /// </summary>
        public bool Move(PlacedObjectRecord moved)
        {
            if (moved == null)
                throw new ArgumentNullException(nameof(moved));
            if (_added.ContainsKey(moved.Id))
                _added[moved.Id] = moved;
            else if (moved.Id.Namespace != Namespace && !_removed.Contains(moved.Id))
                _moved[moved.Id] = moved;
            else
                return false;
            Moved?.Invoke(moved);
            return true;
        }

        /// <summary>An id for a new placed object, never given before by this world's changes, so nothing stale names it.</summary>
        public RecordId NewId() => new RecordId(Namespace, "placed/" + ++_lastId);

        /// <summary>
        /// Puts one <paramref name="baseObject"/> (an item dropped, an arrow landed) into <paramref name="space"/> at
        /// <paramref name="position"/>, turned <paramref name="rotation"/>, at its own size: the placed object it now is.
        /// </summary>
        public PlacedObjectRecord Place(RecordId baseObject, RecordId space, System.Numerics.Vector3 position, System.Numerics.Quaternion rotation)
        {
            var placed = new PlacedObjectRecord(NewId(), baseObject, space, position, rotation, 1f);
            Add(placed);
            return placed;
        }

        /// <summary>Puts <paramref name="placed"/>, made while playing (<see cref="NewId"/>), into the world.</summary>
        public void Add(PlacedObjectRecord placed)
        {
            if (placed == null)
                throw new ArgumentNullException(nameof(placed));
            if (placed.Id.Namespace != Namespace)
                throw new ArgumentException($"A placed object playing makes is named by {nameof(NewId)}; '{placed.Id}' is a plugin's.", nameof(placed));
            if (_added.ContainsKey(placed.Id))
                throw new ArgumentException($"'{placed.Id}' is in the world already.", nameof(placed));

            _added.Add(placed.Id, placed);
            Added?.Invoke(placed);
        }

        /// <summary>Takes the placed object <paramref name="placed"/>, built or added, out of the world; false if it already was.</summary>
        public bool Remove(RecordId placed)
        {
            if (placed.IsDefault)
                throw new ArgumentException("Name the placed object to remove.", nameof(placed));
            // What playing added simply goes again; the world's own is remembered as taken.
            if (!_added.Remove(placed) && (placed.Namespace == Namespace || !_removed.Add(placed)))
                return false;
            _moved.Remove(placed);
            Removed?.Invoke(placed);
            return true;
        }

        /// <summary>Lets every change go, as going back to editing does.</summary>
        public void Clear()
        {
            if (_removed.Count == 0 && _added.Count == 0 && _moved.Count == 0)
                return;
            _removed.Clear();
            _added.Clear();
            _moved.Clear();
            Cleared?.Invoke();
        }
    }
}
