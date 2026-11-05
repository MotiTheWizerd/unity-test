using System;
using Forge.Data.Terrain;

namespace Forge.Streaming
{
    /// <summary>
    /// The far tile grid: the land cut into squares of <c>chunksPerTile</c> chunks, each sampled every <c>step</c>
    /// meters, so distant land can be drawn as a few coarse meshes instead of many full chunks. Engine-free.
    /// </summary>
    public static class FarTiles
    {
        /// <summary>The tile holding <paramref name="chunk"/>.</summary>
        public static ChunkCoord TileOf(ChunkCoord chunk, int chunksPerTile)
        {
            RequireChunksPerTile(chunksPerTile);
            return new ChunkCoord(FloorDiv(chunk.X, chunksPerTile), FloorDiv(chunk.Z, chunksPerTile));
        }

        /// <summary>
        /// Samples the land of one tile. <paramref name="terrainAt"/> answers the heights of a chunk, or null where
        /// there is none; it is asked once per chunk of the tile, and once more for each neighbour that shares the
        /// tile's east or north edge (their samples are the tile's last row and column where the tile's own chunk is
        /// missing). <paramref name="step"/> must divide the chunk evenly and be a whole multiple of every chunk's
        /// own sample spacing, so each far sample is one of the chunk's samples, never an interpolation.
        /// </summary>
        public static FarTile Sample(ChunkCoord tile, int chunksPerTile, float step, Func<ChunkCoord, ChunkHeights> terrainAt)
        {
            RequireChunksPerTile(chunksPerTile);
            if (terrainAt == null)
                throw new ArgumentNullException(nameof(terrainAt));
            var cellsPerChunk = WorldGrid.ChunkSize / step;
            if (!(step > 0f) || Math.Abs(cellsPerChunk - Math.Round(cellsPerChunk)) > 1e-4)
                throw new ArgumentOutOfRangeException(nameof(step), step, $"Step must divide the chunk ({WorldGrid.ChunkSize} m) evenly.");

            var cells = (int)Math.Round(cellsPerChunk);
            var resolution = chunksPerTile * cells + 1;
            var heights = new float[resolution * resolution];
            var present = new bool[chunksPerTile * chunksPerTile];
            var first = new ChunkCoord(tile.X * chunksPerTile, tile.Z * chunksPerTile);

            // Each chunk writes its own samples, including its east and north edges (neighbours share them, so the
            // order doesn't matter), and a missing chunk writes nothing: a boundary sample next to a hole keeps the
            // existing chunk's height. Neighbours east and north of the tile fill the last column and row.
            for (var cz = 0; cz <= chunksPerTile; cz++)
            for (var cx = 0; cx <= chunksPerTile; cx++)
            {
                var insideX = cx < chunksPerTile;
                var insideZ = cz < chunksPerTile;
                var field = terrainAt(new ChunkCoord(first.X + cx, first.Z + cz));
                if (field == null)
                    continue;
                if (insideX && insideZ)
                    present[cz * chunksPerTile + cx] = true;

                var stride = Stride(field, step);
                var samples = field.Load();
                // The samples of this chunk that fall inside the tile's grid: all of them for an inside chunk, only
                // the west column / south row for a neighbour beyond the tile's edge.
                var countX = insideX ? cells + 1 : 1;
                var countZ = insideZ ? cells + 1 : 1;
                for (var sz = 0; sz < countZ; sz++)
                for (var sx = 0; sx < countX; sx++)
                {
                    var gx = cx * cells + sx;
                    var gz = cz * cells + sz;
                    if (gx > resolution - 1 || gz > resolution - 1)
                        continue;
                    heights[gz * resolution + gx] = samples[sx * stride, sz * stride];
                }
            }

            return new FarTile(tile, chunksPerTile, step, heights, present);
        }

        static int Stride(ChunkHeights field, float step)
        {
            var spacing = WorldGrid.SpacingOf(field.Resolution);
            var stride = step / spacing;
            if (Math.Abs(stride - Math.Round(stride)) > 1e-4 || stride < 1f)
                throw new ArgumentOutOfRangeException(nameof(step), step, $"Step must be a whole multiple of the chunk's {spacing} m sample spacing.");
            return (int)Math.Round(stride);
        }

        static void RequireChunksPerTile(int chunksPerTile)
        {
            if (chunksPerTile < 1)
                throw new ArgumentOutOfRangeException(nameof(chunksPerTile), chunksPerTile, "A tile holds at least one chunk.");
        }

        static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
    }
}
