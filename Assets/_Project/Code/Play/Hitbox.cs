using Forge.Game.Combat;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// One part of an actor's body as a blow or a shot meets it (its <see cref="Part"/>): a capsule hung from the part's
    /// bone (<see cref="Hitboxes"/>). Whose it is, the body it hangs under says (<see cref="ActorLink"/>). Runtime-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hitbox : MonoBehaviour
    {
        public BodyPart Part { get; internal set; }

        /// <summary>The part of a body <paramref name="collider"/> is, or <see cref="BodyPart.None"/> when it is no hitbox.</summary>
        public static BodyPart PartOf(Collider collider)
        {
            var hitbox = collider != null ? collider.GetComponent<Hitbox>() : null;
            return hitbox != null ? hitbox.Part : BodyPart.None;
        }
    }
}
