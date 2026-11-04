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
        /// <summary>
        /// Height of the starting land, in meters. Heights can't go below 0, so the land starts well above it:
        /// room to dig lakes and valleys down, and the rest of <see cref="WorldGrid.MaxTerrainHeight"/> to raise hills.
        /// </summary>
        public const float BaseHeight = 50f;

        /// <summary>Flat land: shaping it is the editor's job.</summary>
        public static float HeightAt(float worldX, float worldZ) => BaseHeight;

        public static TerrainChunkRecord Chunk(string pluginId, ChunkCoord coord)
        {
            var step = WorldGrid.ChunkSize / (WorldGrid.DefaultHeightmapResolution - 1);
            var heights = HeightField.FromFunction(
                (x, z) => HeightAt(coord.X * WorldGrid.ChunkSize + x * step, coord.Z * WorldGrid.ChunkSize + z * step));
            return new TerrainChunkRecord(TerrainChunkRecord.IdFor(pluginId, coord), coord, heights);
        }
    }
}
