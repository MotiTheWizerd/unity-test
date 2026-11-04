using System;
using Forge.Data.Terrain;

namespace Forge.Data.Records
{
    /// <summary>
    /// Heights for one chunk of a worldspace's grid. The record name is derived from the space and the coordinate,
    /// so a mod changes terrain by overriding the master's chunk record rather than adding a competing one, and
    /// adds land to any space by defining a chunk at a new coordinate in its own namespace.
    /// </summary>
    public sealed class TerrainChunkRecord : Record
    {
        public TerrainChunkRecord(RecordId id, RecordId space, ChunkCoord coord, HeightField heights)
            : base(id)
        {
            if (space.IsDefault)
                throw new ArgumentException("The space the chunk belongs to is required.", nameof(space));

            var expectedName = NameFor(space, coord);
            if (id.Name != expectedName)
                throw new ArgumentException($"Terrain chunk {coord} of {space} must be named '{expectedName}', not '{id.Name}'.", nameof(id));

            Space = space;
            Coord = coord;
            Heights = heights ?? throw new ArgumentNullException(nameof(heights));
        }

        /// <summary>The worldspace this chunk is land of.</summary>
        public RecordId Space { get; }

        public ChunkCoord Coord { get; }

        public HeightField Heights { get; }

        public TerrainChunkRecord WithHeights(HeightField heights) => new TerrainChunkRecord(Id, Space, Coord, heights);

        public static RecordId IdFor(string ns, RecordId space, ChunkCoord coord) => new RecordId(ns, NameFor(space, coord));

        // e.g. "terrain/forge.core/space/main/c-1_0"; the "c" prefix keeps negative coordinates valid record names.
        static string NameFor(RecordId space, ChunkCoord coord) => $"terrain/{space.Namespace}/{space.Name}/c{coord.X}_{coord.Z}";
    }
}
