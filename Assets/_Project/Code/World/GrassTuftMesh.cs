using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// The one mesh every grass tuft is drawn with: a few tapered blades, one unit high, standing around the origin and
    /// leaning out a little. UV.y runs from the root (0) to the tip (1); the grass shader shades and sways by it, and
    /// scales, turns and places each tuft from its instance data.
    /// </summary>
    public static class GrassTuftMesh
    {
        // 6 blades of 2 segments: 18 triangles a tuft, cheap enough for a meadow of a hundred thousand.
        const int Blades = 6;
        const int Segments = 2;
        const float Width = 0.11f;   // blade width at the root, in tuft heights
        const float Spread = 0.22f;  // how far from the middle the blades stand, in tuft heights
        const float Lean = 0.28f;    // how far out the tips lean, in tuft heights

        public static Mesh Build()
        {
            var vertices = new Vector3[Blades * (Segments * 2 + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Blades * (Segments * 2 - 1) * 3 + Blades * 3];
            var v = 0;
            var t = 0;

            for (var blade = 0; blade < Blades; blade++)
            {
                // Spread the blades around the middle at uneven angles and heights, so a tuft has no visible pattern.
                var angle = blade * (Mathf.PI * 2f / Blades) + (blade % 2) * 0.6f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var across = new Vector3(-outward.z, 0f, outward.x);
                var root = outward * (Spread * (0.4f + 0.6f * ((blade * 7) % Blades) / Blades));
                var height = 0.75f + 0.25f * ((blade * 5) % Blades) / (Blades - 1);

                var first = v;
                for (var s = 0; s < Segments; s++)
                {
                    var along = (float)s / Segments;
                    var center = root + outward * (Lean * along * along) + Vector3.up * (height * along);
                    var half = across * (Width * 0.5f * (1f - along));
                    vertices[v] = center - half;
                    uvs[v++] = new Vector2(0f, along);
                    vertices[v] = center + half;
                    uvs[v++] = new Vector2(1f, along);
                }

                vertices[v] = root + outward * Lean + Vector3.up * height;
                uvs[v++] = new Vector2(0.5f, 1f);

                for (var s = 0; s < Segments - 1; s++)
                {
                    var a = first + s * 2;
                    triangles[t++] = a;
                    triangles[t++] = a + 2;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 2;
                    triangles[t++] = a + 3;
                }

                var last = first + (Segments - 1) * 2;
                triangles[t++] = last;
                triangles[t++] = v - 1;
                triangles[t++] = last + 1;
            }

            var mesh = new Mesh { name = "Grass Tuft", vertices = vertices, uv = uvs };
            System.Array.Resize(ref triangles, t);
            mesh.triangles = triangles;
            // Big bounds: the shader moves every vertex, so the mesh's own bounds mean nothing (draws are culled per chunk).
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
