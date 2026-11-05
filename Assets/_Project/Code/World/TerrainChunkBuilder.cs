using Forge.Data.Records;
using Forge.Data.Terrain;
using UnityEngine;

namespace Forge.World
{
    /// <summary>Turns a <see cref="TerrainChunkRecord"/> into a Unity Terrain and keeps its heights and paint in sync.</summary>
    static class TerrainChunkBuilder
    {
        /// <param name="material">The terrain material (Materials/Terrain). Set explicitly, not left to the render
        /// pipeline's default, which a build may strip.</param>
        /// <param name="ground">How ground layers look; null draws the land unpainted (the material's own look).</param>
        public static GameObject Build(TerrainChunkRecord chunk, Transform parent, Material material, GroundLooks ground)
        {
            var data = new TerrainData { name = chunk.Id.ToString() };
            ApplyHeights(data, chunk.Heights.Load());
            ApplyPaint(data, chunk.Paint, ground);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = chunk.Id.ToString();
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = material;
            // Lets Unity connect neighbouring chunks so their LOD edges match. The
            // streamer marks connectivity dirty whenever chunks come and go.
            terrain.allowAutoConnect = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(chunk.Coord.X * WorldGrid.ChunkSize, 0f, chunk.Coord.Z * WorldGrid.ChunkSize);
            return go;
        }

        /// <summary>Brings a built chunk from <paramref name="previous"/> to <paramref name="current"/>, touching only what changed.</summary>
        public static void Update(GameObject terrainObject, TerrainChunkRecord previous, TerrainChunkRecord current, GroundLooks ground)
        {
            var data = terrainObject.GetComponent<Terrain>().terrainData;
            if (!ReferenceEquals(previous?.Heights, current.Heights))
                ApplyHeights(data, current.Heights.Load());
            if (previous == null || !ReferenceEquals(previous.Paint, current.Paint))
                ApplyPaint(data, current.Paint, ground);
        }

        /// <summary>Writes heights into an existing terrain (also used for live sculpt previews).</summary>
        public static void ApplyHeights(GameObject terrainObject, HeightField field) =>
            ApplyHeights(terrainObject.GetComponent<Terrain>().terrainData, field);

        /// <summary>Writes paint into an existing terrain (also used for live paint previews); null shows the base ground.</summary>
        public static void ApplyPaint(GameObject terrainObject, ChunkPaint paint, GroundLooks ground) =>
            ApplyPaint(terrainObject.GetComponent<Terrain>().terrainData, paint, ground);

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

        // One terrain layer per ground layer the chunk names, and its weights as the alphamap (indexed [z, x, layer]);
        // unpainted ground is the base layer alone.
        static void ApplyPaint(TerrainData data, ChunkPaint paint, GroundLooks ground)
        {
            if (ground?.Base == null)
                return;

            const int resolution = GroundPaint.Resolution;
            if (data.alphamapResolution != resolution)
                data.alphamapResolution = resolution;

            if (paint == null)
            {
                data.terrainLayers = new[] { ground.Base };
                data.SetAlphamaps(0, 0, Filled(resolution));
                return;
            }

            var weights = paint.Load();
            var layers = new TerrainLayer[weights.Layers.Count];
            for (var i = 0; i < layers.Length; i++)
                layers[i] = ground.Of(weights.Layers[i]);
            data.terrainLayers = layers;

            var alphamap = new float[resolution, resolution, layers.Length];
            for (var layer = 0; layer < layers.Length; layer++)
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                alphamap[z, x, layer] = weights[layer, x, z] / (float)GroundPaint.Full;
            data.SetAlphamaps(0, 0, alphamap);
        }

        static float[,,] Filled(int resolution)
        {
            var alphamap = new float[resolution, resolution, 1];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                alphamap[z, x, 0] = 1f;
            return alphamap;
        }
    }
}
