using Forge.Data.Records;
using Forge.Data.Terrain;
using UnityEngine;

namespace Forge.World
{
    /// <summary>Turns a <see cref="TerrainChunkRecord"/> into a Unity Terrain and keeps its heights in sync.</summary>
    static class TerrainChunkBuilder
    {
        public static GameObject Build(TerrainChunkRecord chunk, Transform parent)
        {
            var data = new TerrainData { name = chunk.Id.ToString() };
            ApplyHeights(data, chunk.Heights);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = chunk.Id.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(chunk.Coord.X * WorldGrid.ChunkSize, 0f, chunk.Coord.Z * WorldGrid.ChunkSize);
            return go;
        }

        /// <summary>Writes heights into an existing terrain (also used for live sculpt previews).</summary>
        public static void ApplyHeights(GameObject terrainObject, HeightField field) =>
            ApplyHeights(terrainObject.GetComponent<Terrain>().terrainData, field);

        public static void Destroy(GameObject terrainObject)
        {
            var terrain = terrainObject.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
                Object.Destroy(terrain.terrainData);
            Object.Destroy(terrainObject);
        }

        static void ApplyHeights(TerrainData data, HeightField field)
        {
            var resolution = field.Resolution;
            if (data.heightmapResolution != resolution)
            {
                data.heightmapResolution = resolution;
                // Size must be set after the resolution, which resets it.
                data.size = new Vector3(WorldGrid.ChunkSize, WorldGrid.MaxTerrainHeight, WorldGrid.ChunkSize);
            }

            // Unity expects normalized heights indexed [z, x].
            var heights = new float[resolution, resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                heights[z, x] = field[x, z] / WorldGrid.MaxTerrainHeight;
            data.SetHeights(0, 0, heights);
        }
    }
}
