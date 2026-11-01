using System;
using Forge.Data.Terrain;

namespace Forge.Data.Records
{
    /// <summary>
    /// Heights for one chunk of the world grid. The record name is derived from
    /// the coordinate, so a mod changes terrain by overriding the master's chunk
    /// record rather than adding a competing one.
    /// </summary>
    public sealed class TerrainChunkRecord : Record
    {
        public TerrainChunkRecord(RecordId id, ChunkCoord coord, HeightField heights)
            : base(id)
        {
            var expectedName = NameFor(coord);
            if (id.Name != expectedName)
                throw new ArgumentException($"Terrain chunk {coord} must be named '{expectedName}', not '{id.Name}'.", nameof(id));

            Coord = coord;
            Heights = heights ?? throw new ArgumentNullException(nameof(heights));
        }

        public ChunkCoord Coord { get; }

        public HeightField Heights { get; }

        public static RecordId IdFor(string ns, ChunkCoord coord) => new RecordId(ns, NameFor(coord));

        // "c" prefix keeps negative coordinates valid record names, e.g. "terrain/c-1_0".
        static string NameFor(ChunkCoord coord) => $"terrain/c{coord.X}_{coord.Z}";
    }
}
