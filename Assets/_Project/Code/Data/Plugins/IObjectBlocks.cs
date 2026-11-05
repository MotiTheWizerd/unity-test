using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// A plugin's placed objects, a block per chunk (<see cref="ObjectBlockKey"/>), read a block at a time. In memory for
    /// a plugin being edited or built; stored in an objects file next to a loaded plugin, read when a block is first
    /// needed. What is known without reading any block: which blocks there are and how many objects each holds,
    /// which block an id is in, and which ids override another plugin's objects.
    /// </summary>
    public interface IObjectBlocks
    {
        /// <summary>Every block with objects, sorted by key.</summary>
        IReadOnlyList<ObjectBlockKey> Keys { get; }

        /// <summary>Objects in all blocks together.</summary>
        int TotalCount { get; }

        /// <summary>Ids of objects in another plugin's namespace: overrides of a master's objects. Usually few.</summary>
        IReadOnlyCollection<RecordId> Overrides { get; }

        /// <summary>How many objects block <paramref name="key"/> holds; 0 for a block that isn't there.</summary>
        int CountOf(ObjectBlockKey key);

        /// <summary>The objects of block <paramref name="key"/>, sorted by id; empty for a block that isn't there.</summary>
        /// <exception cref="DataValidationException">A stored block is damaged or breaks the plugin's rules.</exception>
        IReadOnlyList<PlacedObjectRecord> Read(ObjectBlockKey key);

        /// <summary>The block holding the object <paramref name="id"/>, without reading any block.</summary>
        bool TryLocate(RecordId id, out ObjectBlockKey key);
    }
}
