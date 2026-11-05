using System.Collections.Generic;
using Forge.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World
{
    /// <summary>
    /// Turns a <see cref="FarTile"/> into one coarse mesh: a vertex per far sample, a quad per cell that lies on land.
    /// No collider: picks go through to the real terrain or to nothing, and nothing seats on far land.
    /// </summary>
    static class FarTerrainMesh
    {
        /// <summary>Builds the tile's object under <paramref name="parent"/> at its world corner, <paramref name="drop"/> meters down.</summary>
        public static GameObject Build(FarTile tile, Transform parent, Material material, float drop)
        {
            var go = new GameObject($"far {tile.Coord}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(tile.Coord.X * tile.Size, -drop, tile.Coord.Z * tile.Size);
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(tile);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            // Far land is a backdrop: it casts no shadows (the near terrain does), but takes the sun's.
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static void Destroy(GameObject tileObject)
        {
            if (tileObject.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                Object.Destroy(filter.sharedMesh);
            Object.Destroy(tileObject);
        }

        static Mesh Mesh(FarTile tile)
        {
            var resolution = tile.Resolution;
            var vertices = new Vector3[resolution * resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                vertices[z * resolution + x] = new Vector3(x * tile.Step, tile[x, z], z * tile.Step);

            var triangles = new List<int>(tile.Cells * tile.Cells * 6);
            for (var z = 0; z < tile.Cells; z++)
            for (var x = 0; x < tile.Cells; x++)
            {
                if (!tile.CellHasLand(x, z))
                    continue;
                var a = z * resolution + x;
                var b = a + resolution;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }

            var mesh = new Mesh { name = $"far {tile.Coord}" };
            mesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
