using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.UnityEditor
{
    /// <summary>
    /// The land every generated world starts from. It is defined in world space, so chunks from different
    /// generators (or plugins) share edges exactly.
    /// </summary>
    static class ProceduralTerrain
    {
        /// <summary>Height of the starting land, in meters.</summary>
        public const float BaseHeight = WorldGrid.DefaultLandHeight;

        /// <summary>Flat land: shaping it is the editor's job.</summary>
        public static float HeightAt(float worldX, float worldZ) => BaseHeight;

        public static TerrainChunkRecord Chunk(string pluginId, RecordId space, ChunkCoord coord)
        {
            var step = WorldGrid.ChunkSize / (WorldGrid.DefaultHeightmapResolution - 1);
            var heights = HeightField.FromFunction(
                (x, z) => HeightAt(coord.X * WorldGrid.ChunkSize + x * step, coord.Z * WorldGrid.ChunkSize + z * step));
            return new TerrainChunkRecord(TerrainChunkRecord.IdFor(pluginId, space, coord), space, coord, heights);
        }
    }
}
