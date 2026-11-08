using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The model contract of an arrow's look (anything a bow shoots): it points +Y, its tail (the nock) at the -Y end.
    /// How far each end lies from its pivot, read off its meshes, in its own units.
    /// </summary>
    public static class ArrowLook
    {
        /// <summary>How far behind its pivot its tail lies (along -Y); 0 without meshes.</summary>
        public static float Tail(GameObject look) => -Reach(look, false);

        /// <summary>How far ahead of its pivot its point lies (along +Y); 0 without meshes.</summary>
        public static float Point(GameObject look) => Reach(look, true);

        // The farthest corner of its meshes' bounds along +Y (or the nearest, for the tail), in the look's frame.
        static float Reach(GameObject look, bool point)
        {
            var found = false;
            var reach = 0f;
            var root = look.transform;
            foreach (var filter in look.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var bounds = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var y = root.InverseTransformPoint(filter.transform.TransformPoint(local)).y;
                    reach = !found ? y : point ? Mathf.Max(reach, y) : Mathf.Min(reach, y);
                    found = true;
                }
            }

            return reach;
        }
    }
}
