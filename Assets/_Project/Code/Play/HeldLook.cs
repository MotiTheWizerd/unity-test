using UnityEngine;

namespace Forge.Play
{
    /// <summary>Where a held thing goes while its holder's weapons are put away.</summary>
    public enum CarriedAt
    {
        /// <summary>In its sheath at the hip, where the draw takes it from (a sword).</summary>
        Hip,

        /// <summary>Across the back (a bow, as in Skyrim).</summary>
        Back,
    }

    /// <summary>
    /// How a model asset (Content/Models) is held and carried, as its {Name}.json says; the model build puts it on the
    /// prefab. <see cref="FacesHolder"/>: the side that must face whoever holds it, in the model's own frame (a bow's
    /// string, for the other hand to take); the hand turns it about its length to face the body, and on the back it lies
    /// flat with that side down across the back. <see cref="Carried"/>: where it goes while put away. A model without one is held any way round and carried at
    /// the hip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeldLook : MonoBehaviour
    {
        [SerializeField, Tooltip("The side that faces whoever holds it, in the model's frame (a bow's string: -X); zero: any side.")]
        Vector3 _facesHolder;

        [SerializeField, Tooltip("Where it goes while its holder's weapons are put away.")]
        CarriedAt _carried;

        /// <summary>The side that faces whoever holds it (a unit vector in the model's frame); null when any side may.</summary>
        public Vector3? FacesHolder => _facesHolder.sqrMagnitude > 1e-8f ? _facesHolder.normalized : (Vector3?)null;

        public CarriedAt Carried => _carried;

        /// <summary>Sets both, as the model build reads them from the json.</summary>
        public void Configure(Vector3? facesHolder, CarriedAt carried)
        {
            _facesHolder = facesHolder ?? Vector3.zero;
            _carried = carried;
        }
    }
}
