using System;
using System.Collections.Generic;
using Forge.Data;

namespace Forge.Game.World
{
    /// <summary>
    /// What playing has changed in the world, kept beside it the way a saved game keeps its changes over the load order
    /// (Bethesda's changed references): the world's records stay exactly as built, and whatever shows the world while
    /// playing lays these on top. For now the placed objects taken out of it; dropped items, opened doors and emptied
    /// containers will join them, and the save game will write it all. Engine-free.
    /// </summary>
    public sealed class WorldChanges
    {
        readonly HashSet<RecordId> _removed = new HashSet<RecordId>();

        /// <summary>Raised with a placed object as it is taken out of the world.</summary>
        public event Action<RecordId> Removed;

        /// <summary>Raised when every change is let go: the world is as built again.</summary>
        public event Action Cleared;

        /// <summary>The placed objects taken out of the world.</summary>
        public IReadOnlyCollection<RecordId> RemovedObjects => _removed;

        public bool IsRemoved(RecordId placed) => _removed.Contains(placed);

        /// <summary>Takes the placed object <paramref name="placed"/> out of the world; false if it already was.</summary>
        public bool Remove(RecordId placed)
        {
            if (placed.IsDefault)
                throw new ArgumentException("Name the placed object to remove.", nameof(placed));
            if (!_removed.Add(placed))
                return false;
            Removed?.Invoke(placed);
            return true;
        }

        /// <summary>Lets every change go, as going back to editing does.</summary>
        public void Clear()
        {
            if (_removed.Count == 0)
                return;
            _removed.Clear();
            Cleared?.Invoke();
        }
    }
}
