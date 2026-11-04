using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.World
{
    /// <summary>
    /// The sea of the space on show: one flat surface at its water level that follows the camera across the land and
    /// out past its edge to the far clip, as large as the view; the land hides it wherever the ground stands higher.
    /// Presentation only: the sea is the space's <c>WaterLevel</c>, and depth questions go to the engine-free
    /// <c>WaterMap</c>, never to this object. It has no collider, so picks go through to the ground beneath.
    /// </summary>
    public sealed class SeaSurface : MonoBehaviour
    {
        Mesh _mesh;
        float _size;

        /// <summary>A hidden sea under <paramref name="parent"/>; call <see cref="Show"/> to raise it to a level.</summary>
        /// <param name="size">Edge length in meters; twice the camera's far clip covers everything it can see.</param>
        public static SeaSurface Create(Transform parent, Material material, float size)
        {
            var go = new GameObject("Sea");
            go.transform.SetParent(parent, false);
            go.SetActive(false);

            var sea = go.AddComponent<SeaSurface>();
            sea._size = size;
            sea._mesh = Quad(size);
            go.AddComponent<MeshFilter>().sharedMesh = sea._mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return sea;
        }

        /// <summary>Shows the sea at <paramref name="level"/> meters, or hides it when there is none.</summary>
        public void Show(float? level)
        {
            gameObject.SetActive(level.HasValue);
            if (level.HasValue)
            {
                var position = transform.localPosition;
                transform.localPosition = new Vector3(position.x, level.Value, position.z);
            }
        }

        /// <summary>
        /// Keeps the sea centered under <paramref name="focus"/> (the camera), so it reaches the horizon wherever the
        /// camera flies. The shader works in world space, so moving the surface never moves its ripples.
        /// </summary>
        public void Follow(Vector3 focus)
        {
            if (!gameObject.activeSelf)
                return;

            // Step in whole tenths of the size, so the surface doesn't move every frame for nothing.
            var step = _size / 10f;
            var local = transform.parent != null ? transform.parent.InverseTransformPoint(focus) : focus;
            transform.localPosition = new Vector3(Mathf.Round(local.x / step) * step, transform.localPosition.y, Mathf.Round(local.z / step) * step);
        }

        // One quad facing up; the ripples are in the shader's normals, so there is nothing for more vertices to do.
        static Mesh Quad(float size)
        {
            var half = size / 2f;
            var mesh = new Mesh { name = "Sea" };
            mesh.SetVertices(new[] { new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(half, 0f, -half) });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }
    }
}
