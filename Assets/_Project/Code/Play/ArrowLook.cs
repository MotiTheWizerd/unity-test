using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The model contract of an arrow's look (anything a bow shoots): its model (<see cref="ModelGrip.Of"/>, inside the
    /// asset's root, which may lie it flat for the world) points +Y, its tail (the nock) at the -Y end. Lays a look along
    /// a line by that model, whatever its root does: by its tail (on the string) or by its point (in flight).
    /// </summary>
    public static class ArrowLook
    {
        /// <summary>How far behind its model's pivot its tail lies (along -Y), meters; 0 without meshes.</summary>
        public static float Tail(GameObject look) => -Reach(look, false);

        /// <summary>How far ahead of its model's pivot its point lies (along +Y), meters; 0 without meshes.</summary>
        public static float Point(GameObject look) => Reach(look, true);

        /// <summary>Lays <paramref name="look"/> with its tail at <paramref name="tail"/>, its model turned to <paramref name="rotation"/> (+Y the way it points).</summary>
        public static void LayByTail(GameObject look, Vector3 tail, Quaternion rotation) =>
            Lay(look, tail + rotation * Vector3.up * Tail(look), rotation);

        /// <summary>Lays <paramref name="look"/> with its point at <paramref name="point"/>, pointing along <paramref name="heading"/>.</summary>
        public static void LayByPoint(GameObject look, Vector3 point, Vector3 heading)
        {
            var rotation = Quaternion.FromToRotation(Vector3.up, heading);
            Lay(look, point - rotation * Vector3.up * Point(look), rotation);
        }

        // Its model's pivot at `pivot`, turned to `rotation`, the root carried along.
        static void Lay(GameObject look, Vector3 pivot, Quaternion rotation)
        {
            var root = look.transform;
            var model = ModelGrip.Of(look);
            var inRoot = Quaternion.Inverse(root.rotation) * model.rotation;
            root.rotation = rotation * Quaternion.Inverse(inRoot);
            root.position += pivot - model.position;
        }

        // The farthest corner of its meshes' bounds along its model's +Y (or the nearest, for the tail), in meters.
        static float Reach(GameObject look, bool point)
        {
            var found = false;
            var reach = 0f;
            var model = ModelGrip.Of(look);
            foreach (var filter in look.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var bounds = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var y = model.InverseTransformPoint(filter.transform.TransformPoint(local)).y;
                    reach = !found ? y : point ? Mathf.Max(reach, y) : Mathf.Min(reach, y);
                    found = true;
                }
            }

            return reach * model.lossyScale.y;
        }
    }
}
