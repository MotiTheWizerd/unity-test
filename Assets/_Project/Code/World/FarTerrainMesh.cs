using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World
{
    /// <summary>
    /// Turns a <see cref="FarTile"/> into one coarse mesh: a vertex per far sample, colored as its ground looks from
    /// afar, and a quad per cell that lies on land. Where the land ends (<see cref="FarTile.Edges"/>) a skirt hangs
    /// from the real terrain's edge down past the far land, in a darker earth tone, so the map's edge reads as a solid
    /// slab instead of two thin sheets with sky between them. No collider: picks go through to the real terrain or to
    /// nothing, and nothing seats on far land.
    /// </summary>
    static class FarTerrainMesh
    {
        /// <summary>How far below the far land the skirt reaches, in meters.</summary>
        const float SkirtBelow = 4f;

        /// <summary>The skirt is its ground's color this much darker: a cut face of earth, in shade.</summary>
        const float SkirtShade = 0.55f;

        /// <summary>Builds the tile's object under <paramref name="parent"/> at its world corner, <paramref name="drop"/> meters down.</summary>
        /// <param name="colorOf">A sample's color (linear) from its ground layer (null: unpainted).</param>
        public static GameObject Build(FarTile tile, Transform parent, Material material, float drop, Func<RecordId?, Color> colorOf)
        {
            var go = new GameObject($"far {tile.Coord}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(tile.Coord.X * tile.Size, -drop, tile.Coord.Z * tile.Size);
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(tile, drop, colorOf);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            // Far land is a backdrop: it casts no shadows (the near terrain does), but takes the sun's.
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static void Destroy(GameObject tileObject)
        {
            if (tileObject.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                UnityEngine.Object.Destroy(filter.sharedMesh);
            UnityEngine.Object.Destroy(tileObject);
        }

        static Mesh Mesh(FarTile tile, float drop, Func<RecordId?, Color> colorOf)
        {
            var resolution = tile.Resolution;
            var vertices = new List<Vector3>(new Vector3[resolution * resolution]);
            var colors = new List<Color>(new Color[vertices.Count]);
            // Few layers show across a tile; each color is looked up once.
            var cache = new Dictionary<RecordId, Color>();
            var unpainted = colorOf(null);
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var i = z * resolution + x;
                vertices[i] = new Vector3(x * tile.Step, tile[x, z], z * tile.Step);
                colors[i] = ColorAt(tile, x, z, unpainted, cache, colorOf);
            }

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

            foreach (var edge in tile.Edges)
                AddSkirt(edge, tile, drop, vertices, colors, triangles, (x, z) => ColorAt(tile, x, z, unpainted, cache, colorOf));

            var mesh = new Mesh { name = $"far {tile.Coord}" };
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A sample's color: unpainted, or its ground layer's (each layer looked up once per tile).
        static Color ColorAt(FarTile tile, int x, int z, Color unpainted, Dictionary<RecordId, Color> cache, Func<RecordId?, Color> colorOf)
        {
            if (!(tile.GroundAt(x, z) is RecordId layer))
                return unpainted;
            if (!cache.TryGetValue(layer, out var color))
                cache.Add(layer, color = colorOf(layer));
            return color;
        }

        // A wall of quads down the edge: tops at the real land's height (the object sits `drop` below it), bottoms
        // SkirtBelow under the far land. Its vertices are its own, so its normals face out, away from the land: with
        // the land on the edge's left, (along, down) quads wind towards the outside.
        static void AddSkirt(LandEdge edge, FarTile tile, float drop, List<Vector3> vertices, List<Color> colors, List<int> triangles,
            Func<int, int, Color> colorAt)
        {
            var first = vertices.Count;
            for (var i = 0; i < edge.Heights.Length; i++)
            {
                var x = edge.StartX + edge.DirectionX * edge.Spacing * i;
                var z = edge.StartZ + edge.DirectionZ * edge.Spacing * i;
                var height = edge.Heights[i];
                // The skirt is the color of the far sample nearest it, in shade.
                var color = colorAt(Nearest(x, tile), Nearest(z, tile)) * SkirtShade;
                color.a = 1f;
                vertices.Add(new Vector3(x, height + drop, z));
                vertices.Add(new Vector3(x, height - SkirtBelow, z));
                colors.Add(color);
                colors.Add(color);
            }

            for (var i = 0; i < edge.Heights.Length - 1; i++)
            {
                var top = first + i * 2;
                var bottom = top + 1;
                var nextTop = top + 2;
                var nextBottom = top + 3;
                triangles.Add(top); triangles.Add(nextTop); triangles.Add(bottom);
                triangles.Add(nextTop); triangles.Add(nextBottom); triangles.Add(bottom);
            }
        }

        static int Nearest(float meters, FarTile tile) => Math.Max(0, Math.Min(tile.Resolution - 1, (int)Math.Round(meters / tile.Step)));
    }
}
