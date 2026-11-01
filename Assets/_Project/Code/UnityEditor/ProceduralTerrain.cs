using System;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.UnityEditor
{
    /// <summary>
    /// The one height function every generated world uses. It is defined in world
    /// space, so chunks from different generators (or plugins) share edges exactly.
    /// </summary>
    static class ProceduralTerrain
    {
        /// <summary>Smooth rolling hills, in meters.</summary>
        public static float HeightAt(float worldX, float worldZ)
        {
            var h = 20f
                    + 10f * (float)Math.Sin(worldX * 0.045f) * (float)Math.Cos(worldZ * 0.04f)
                    + 4f * (float)Math.Sin((worldX + worldZ) * 0.11f);
            return Math.Max(0f, Math.Min(WorldGrid.MaxTerrainHeight, h));
        }

        public static TerrainChunkRecord Chunk(string pluginId, ChunkCoord coord)
        {
            var step = WorldGrid.ChunkSize / (WorldGrid.DefaultHeightmapResolution - 1);
            var heights = HeightField.FromFunction(
                (x, z) => HeightAt(coord.X * WorldGrid.ChunkSize + x * step, coord.Z * WorldGrid.ChunkSize + z * step));
            return new TerrainChunkRecord(TerrainChunkRecord.IdFor(pluginId, coord), coord, heights);
        }
    }
}
