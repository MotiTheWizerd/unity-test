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
        /// As <see cref="TryGet"/>, but a skinned mesh (a character) is measured as it stands now, posed by its bones,
        /// rather than by the bounds of its rest pose (arms out). Bakes each skinned mesh, so it is for a one-off
        /// measure, not every frame.
        /// </summary>
        /// <returns>False if <paramref name="go"/> has no renderers.</returns>
        public static bool TryGetPosed(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            Mesh baked = null;
            try
            {
                foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                {
                    var part = renderer.bounds;
                    if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                    {
                        if (baked == null)
                            baked = new Mesh { name = "Posed" };
                        // With its scale, so the baked mesh is the drawn one but for the renderer's place and turn.
                        skinned.BakeMesh(baked, true);
                        var transform = skinned.transform;
                        part = Transformed(baked.bounds, Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one));
                    }

                    if (found)
                    {
                        bounds.Encapsulate(part);
                    }
                    else
                    {
                        bounds = part;
                        found = true;
                    }
                }
            }
            finally
            {
                if (baked != null)
                    Object.DestroyImmediate(baked);
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

        // The box around local bounds once moved by matrix.
        static Bounds Transformed(Bounds local, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (var corner = 0; corner < 8; corner++)
            {
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
            }

            return result;
        }
    }
}
