using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Terrain;

namespace Forge.Streaming
{
    /// <summary>
    /// A square of chunks as seen from afar: the land's heights at a coarse <see cref="Step"/> (meters between samples,
    /// far wider than a chunk's own spacing), one sample grid across the whole tile. Chunks that don't exist leave
    /// holes: <see cref="HasChunk"/> and <see cref="CellHasLand"/> tell which cells to draw. Built by
    /// <see cref="FarTiles.Sample"/>; immutable, engine-free.
    /// </summary>
    public sealed class FarTile
    {
        readonly float[] _heights;
        readonly bool[] _chunks;
        readonly RecordId?[] _ground;

        internal FarTile(ChunkCoord coord, int chunksPerTile, float step, float[] heights, bool[] chunks, RecordId?[] ground = null,
            IReadOnlyList<LandEdge> edges = null)
        {
            _ground = ground ?? new RecordId?[heights.Length];
            Edges = edges ?? Array.Empty<LandEdge>();
            Coord = coord;
            ChunksPerTile = chunksPerTile;
            Step = step;
            Resolution = (int)Math.Round(chunksPerTile * WorldGrid.ChunkSize / step) + 1;
            if (heights.Length != Resolution * Resolution)
                throw new ArgumentException($"Expected {Resolution * Resolution} heights but got {heights.Length}.", nameof(heights));
            if (chunks.Length != chunksPerTile * chunksPerTile)
                throw new ArgumentException($"Expected {chunksPerTile * chunksPerTile} chunk flags but got {chunks.Length}.", nameof(chunks));
            _heights = heights;
            _chunks = chunks;
        }

        /// <summary>Tile coordinate: chunk (X · ChunksPerTile, Z · ChunksPerTile) is its south-west chunk.</summary>
        public ChunkCoord Coord { get; }

        public int ChunksPerTile { get; }

        /// <summary>Meters between neighbouring samples.</summary>
        public float Step { get; }

        /// <summary>Samples along each tile edge.</summary>
        public int Resolution { get; }

        /// <summary>Edge length in meters.</summary>
        public float Size => ChunksPerTile * WorldGrid.ChunkSize;

        /// <summary>Cells (quads between samples) along each edge.</summary>
        public int Cells => Resolution - 1;

        /// <summary>The sides of the tile's chunks where the land ends (no chunk beyond them, in this tile or the next).</summary>
        public IReadOnlyList<LandEdge> Edges { get; }

        /// <summary>True when none of the tile's chunks exist: nothing to draw.</summary>
        public bool IsEmpty => Array.IndexOf(_chunks, true) < 0;

        /// <summary>Height in meters at sample (x, z); 0 where no chunk holds the sample.</summary>
        public float this[int x, int z] => _heights[z * Resolution + x];

        /// <summary>The ground layer showing most at sample (x, z); null where the ground is unpainted (or wasn't sampled).</summary>
        public RecordId? GroundAt(int x, int z) => _ground[z * Resolution + x];

        /// <summary>Whether the chunk at local index (x, z), 0 ≤ x, z &lt; ChunksPerTile, exists.</summary>
        public bool HasChunk(int x, int z) => _chunks[z * ChunksPerTile + x];

        /// <summary>Whether cell (x, z), 0 ≤ x, z &lt; Cells, lies on an existing chunk.</summary>
        public bool CellHasLand(int x, int z)
        {
            var cellsPerChunk = Cells / ChunksPerTile;
            return HasChunk(x / cellsPerChunk, z / cellsPerChunk);
        }
    }
}
