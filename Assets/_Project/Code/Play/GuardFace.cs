using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// The face of a raised guard (a shield held up): a box over the held shield's look, a trigger on the
    /// <see cref="HitboxLayer"/>, so it stops no body and no loose thing, but a blade or an arrow that looks for it meets
    /// it before the body behind it (<see cref="BladeSweep"/>, <see cref="ArrowFlights"/>). Whose it is, the body it hangs
    /// under says (<see cref="ActorLink"/>). On only while the guard is up (<see cref="GuardView"/>). Runtime-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuardFace : MonoBehaviour
    {
        // The thinnest a face is, meters: a blade's frame-to-frame lines cross it rather than slip by.
        const float Thinnest = 0.06f;

        BoxCollider _box;

        /// <summary>The face on <paramref name="look"/> (a held shield), made the first time it is asked for: a box over its meshes.</summary>
        public static GuardFace On(GameObject look)
        {
            var face = look.GetComponentInChildren<GuardFace>(true);
            if (face != null)
                return face;

            var holder = new GameObject("Guard Face") { layer = HitboxLayer.Index };
            holder.transform.SetParent(look.transform, false);
            face = holder.AddComponent<GuardFace>();
            face._box = holder.AddComponent<BoxCollider>();
            face._box.isTrigger = true;
            // Moved with the hand every frame: a kinematic body, as a moving collider should have.
            var body = holder.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            face.Fit(look.transform);
            face.Raise(false);
            return face;
        }

        /// <summary>Whether the guard is up: the face is there to be met.</summary>
        public bool Raised => _box != null && _box.enabled;

        /// <summary>Puts the face up (the guard raised) or away.</summary>
        public void Raise(bool up)
        {
            if (_box == null)
                return;
            _box.enabled = up;
            // A look attached again is laid on its model's layer, this with it: a face is met on its own.
            gameObject.layer = HitboxLayer.Index;
        }

        // The box over every mesh of the look, in the look's own space, no thinner than a blade can miss.
        void Fit(Transform look)
        {
            var found = false;
            var bounds = new Bounds();
            foreach (var filter in look.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var mesh = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var inLook = look.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (found)
                        bounds.Encapsulate(inLook);
                    else
                        bounds = new Bounds(inLook, Vector3.zero);
                    found = true;
                }
            }

            var size = found ? bounds.size : Vector3.one * 0.5f;
            _box.center = found ? bounds.center : Vector3.zero;
            _box.size = new Vector3(Mathf.Max(size.x, Thinnest), Mathf.Max(size.y, Thinnest), Mathf.Max(size.z, Thinnest));
        }
    }
}
