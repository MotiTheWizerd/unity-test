using System;
using Forge.Data.Terrain;

namespace Forge.Data.Records
{
    /// <summary>
    /// Heights and ground paint for one chunk of a worldspace's grid, like a Bethesda LAND record. The record name is
    /// derived from the space and the coordinate, so a mod changes terrain by overriding the master's chunk record
    /// rather than adding a competing one, and adds land to any space by defining a chunk at a new coordinate in its
    /// own namespace. Heights and paint live in the plugin's terrain file and are read when first needed
    /// (<see cref="ChunkHeights"/>, <see cref="ChunkPaint"/>), so a record is small.
    /// </summary>
    public sealed class TerrainChunkRecord : Record
    {
        /// <param name="heights">In memory (a <see cref="HeightField"/>) or stored in a terrain file.</param>
        /// <param name="paint">Which ground layers show where; null for unpainted ground (shown as the base ground).</param>
        public TerrainChunkRecord(RecordId id, RecordId space, ChunkCoord coord, ChunkHeights heights, ChunkPaint paint = null)
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
            Paint = paint;
        }

        /// <summary>The worldspace this chunk is land of.</summary>
        public RecordId Space { get; }

        public ChunkCoord Coord { get; }

        /// <summary>The chunk's heights; <see cref="ChunkHeights.Load"/> reads them (from disk the first time).</summary>
        public ChunkHeights Heights { get; }

        /// <summary>The chunk's ground paint; null where it was never painted. <see cref="ChunkPaint.Load"/> reads the weights.</summary>
        public ChunkPaint Paint { get; }

        public TerrainChunkRecord WithHeights(ChunkHeights heights) => new TerrainChunkRecord(Id, Space, Coord, heights, Paint);

        public TerrainChunkRecord WithPaint(ChunkPaint paint) => new TerrainChunkRecord(Id, Space, Coord, Heights, paint);

        public static RecordId IdFor(string ns, RecordId space, ChunkCoord coord) => new RecordId(ns, NameFor(space, coord));

        // e.g. "terrain/forge.core/space/main/c-1_0"; the "c" prefix keeps negative coordinates valid record names.
        static string NameFor(RecordId space, ChunkCoord coord) => $"terrain/{space.Namespace}/{space.Name}/c{coord.X}_{coord.Z}";
    }
}
