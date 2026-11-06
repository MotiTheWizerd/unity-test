using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.Editing.Terrain
{
    /// <summary>An edit of the ground's paint, heights untouched: a brush stroke or a painted shape.</summary>
    public interface IPaintEdit
    {
        /// <summary>The undo step's name, e.g. "Paint (Dirt)".</summary>
        string Description { get; }

        /// <summary>The chunks it painted, with their new paint.</summary>
        IReadOnlyList<TerrainChunkRecord> ChangedRecords();
    }
}
