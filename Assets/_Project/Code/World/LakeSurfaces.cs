using System;
using System.Collections.Generic;
using Forge.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World
{
    /// <summary>
    /// The lakes of the space on show: one flat surface per lake at its level, made of a quad per run of covered
    /// terrain cells, and the land hides it beyond the shore. Plus one preview surface a tool can show while the
    /// user sets a level. Presentation only: which cells are wet is worked out from the data elsewhere.
    /// </summary>
    public sealed class LakeSurfaces : IDisposable
    {
        readonly Transform _root;
        readonly Material _material;
        readonly Dictionary<RecordId, GameObject> _lakes = new Dictionary<RecordId, GameObject>();
        GameObject _preview;

        public LakeSurfaces(Transform parent, Material material)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            _root = new GameObject("Lakes").transform;
            _root.SetParent(parent, false);
            _material = material;
        }

        /// <summary>
        /// Shows lake <paramref name="id"/> over <paramref name="cellRuns"/> (row z, first x, last x, in terrain samples
        /// of <paramref name="spacing"/> meters) at <paramref name="level"/>, replacing what it showed before.
        /// </summary>
        public void Show(RecordId id, float level, float spacing, IEnumerable<(int Z, int FromX, int ToX)> cellRuns)
        {
            Hide(id);
            _lakes.Add(id, Build(id.ToString(), level, spacing, cellRuns));
        }

        public void Hide(RecordId id)
        {
            if (_lakes.TryGetValue(id, out var lake))
            {
                Destroy(lake);
                _lakes.Remove(id);
            }
        }

        public void HideAll()
        {
            foreach (var lake in _lakes.Values)
                Destroy(lake);
            _lakes.Clear();
        }

        /// <summary>Whether lake <paramref name="id"/>'s own surface shows (a preview standing in for it hides it).</summary>
        public void SetVisible(RecordId id, bool visible)
        {
            if (_lakes.TryGetValue(id, out var lake))
                lake.SetActive(visible);
        }

        /// <summary>Shows the preview surface, replacing the previous one.</summary>
        public void ShowPreview(float level, float spacing, IEnumerable<(int Z, int FromX, int ToX)> cellRuns)
        {
            HidePreview();
            _preview = Build("Lake preview", level, spacing, cellRuns);
        }

        public void HidePreview()
        {
            Destroy(_preview);
            _preview = null;
        }

        public void Dispose()
        {
            HideAll();
            HidePreview();
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
        }

        GameObject Build(string name, float level, float spacing, IEnumerable<(int Z, int FromX, int ToX)> cellRuns)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(0f, level, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(name, spacing, cellRuns);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return go;
        }

        static Mesh Mesh(string name, float spacing, IEnumerable<(int Z, int FromX, int ToX)> cellRuns)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var (z, fromX, toX) in cellRuns)
            {
                var first = vertices.Count;
                var (west, east, south, north) = (fromX * spacing, (toX + 1) * spacing, z * spacing, (z + 1) * spacing);
                vertices.Add(new Vector3(west, 0f, south));
                vertices.Add(new Vector3(west, 0f, north));
                vertices.Add(new Vector3(east, 0f, north));
                vertices.Add(new Vector3(east, 0f, south));
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            var mesh = new Mesh { name = name, indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(vertices.ConvertAll(_ => Vector3.up));
            mesh.RecalculateBounds();
            return mesh;
        }

        // Each surface owns its mesh. It is hidden at once, because Destroy only takes effect at the end of the frame.
        static void Destroy(GameObject surface)
        {
            if (surface == null)
                return;
            surface.SetActive(false);
            if (surface.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                UnityEngine.Object.Destroy(filter.sharedMesh);
            UnityEngine.Object.Destroy(surface);
        }
    }
}
