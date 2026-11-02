using UnityEngine;

namespace Forge.World
{
    /// <summary>World-space bounds of everything an object draws, including its child parts.</summary>
    public static class RendererBounds
    {
        /// <returns>False if <paramref name="go"/> has no renderers.</returns>
        public static bool TryGet(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (found)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Bounds of everything the object's meshes draw, in the root's own space (so independent of its
        /// position, rotation and scale). Reads mesh data, so it also works on inactive objects and prefabs.
        /// </summary>
        /// <returns>False if <paramref name="root"/> has no meshes.</returns>
        public static bool TryGetLocal(GameObject root, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            var toRoot = root.transform.worldToLocalMatrix;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                var mesh = filter.sharedMesh.bounds;
                var toRootFromMesh = toRoot * filter.transform.localToWorldMatrix;
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var point = toRootFromMesh.MultiplyPoint3x4(local);
                    if (found)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// How far the pivot sits above the object's lowest drawn point (0 without renderers).
        /// Placing the pivot this far above a surface rests the object on it.
        /// </summary>
        public static float PivotAboveBottom(GameObject go) =>
            TryGet(go, out var bounds) ? go.transform.position.y - bounds.min.y : 0f;
    }
}
