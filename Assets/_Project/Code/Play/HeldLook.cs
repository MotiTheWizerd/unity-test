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
    /// flat with that side down across the back. <see cref="AlongArm"/>: it is strapped to the forearm (a shield), that
    /// way running up the arm from the grip, the side facing its holder to the hand, and it stays as the hand holds it
    /// (<see cref="HoldFinder.Strapped"/>). <see cref="Carried"/>: where it goes while put away. A model without one is
    /// held any way round and carried at the hip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeldLook : MonoBehaviour
    {
        [SerializeField, Tooltip("The side that faces whoever holds it, in the model's frame (a bow's string: -X); zero: any side.")]
        Vector3 _facesHolder;

        [SerializeField, Tooltip("Strapped to the forearm: the way from the grip up the arm, in the model's frame (a shield's middle); zero: held in the hand alone.")]
        Vector3 _alongArm;

        [SerializeField, Tooltip("Where it goes while its holder's weapons are put away.")]
        CarriedAt _carried;

        /// <summary>The side that faces whoever holds it (a unit vector in the model's frame); null when any side may.</summary>
        public Vector3? FacesHolder => Unit(_facesHolder);

        /// <summary>The way from the grip up the forearm it is strapped to (a unit vector in the model's frame); null when it isn't.</summary>
        public Vector3? AlongArm => Unit(_alongArm);

        public CarriedAt Carried => _carried;

        /// <summary>Sets them all, as the model build reads them from the json.</summary>
        public void Configure(Vector3? facesHolder, CarriedAt carried, Vector3? alongArm = null)
        {
            _facesHolder = facesHolder ?? Vector3.zero;
            _alongArm = alongArm ?? Vector3.zero;
            _carried = carried;
        }

        static Vector3? Unit(Vector3 way) => way.sqrMagnitude > 1e-8f ? way.normalized : (Vector3?)null;
    }
}
