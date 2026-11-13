using Forge.Game.Cameras;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where an archer's arrow lies, where it is aimed and what it meets, shared by the arrow on the string
    /// (<see cref="ShotView"/>) and the arrow in flight (<see cref="ArrowFlights"/>): it lies along the draw, from the
    /// drawing (right) hand to the bow (left) hand; it is aimed, as in Skyrim, at what the centre of the view meets, someone
    /// standing there too; it meets the world's things and the people in it where they are drawn
    /// (<see cref="SceneLayers.Strikes"/>), never the archer's own. Scene coordinates.
    /// </summary>
    public static class ArcheryAim
    {
        // Meters the view's line is followed to find what it points at.
        const float Reach = 1000f;

        static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// The line an arrow on <paramref name="model"/>'s string lies along: from the drawing hand's grip
        /// (<paramref name="from"/>), <paramref name="direction"/> to the bow hand's; false without both hands.
        /// </summary>
        public static bool TryDrawLine(CharacterModel model, out Vector3 from, out Vector3 direction)
        {
            from = direction = default;
            if (model == null || !model.TryGrip(true, out var drawing) || !model.TryGrip(false, out var bow))
                return false;
            from = drawing.position;
            var way = bow.position - from;
            direction = way.sqrMagnitude > 1e-6f ? way.normalized : model.Root.transform.forward;
            return true;
        }

        /// <summary>
        /// What <paramref name="view"/>'s line meets first, as an arrow would (<see cref="TryMeet"/>), passing over
        /// <paramref name="archer"/>'s own body; else as far along it as it is followed; when that is behind
        /// <paramref name="from"/> (a wall at the shoulder), straight along the view from there.
        /// </summary>
        public static Vector3 Target(ViewPose view, Vector3 from, CharacterModel archer)
        {
            var origin = view.Position.ToUnity();
            var forward = view.Forward.ToUnity();
            var target = TryMeet(origin, forward, Reach, archer, out var hit) ? hit.point : origin + forward * Reach;
            return Vector3.Dot(target - from, forward) > 0f ? target : from + forward * Reach;
        }

        /// <summary>
        /// The nearest thing an arrow meets <paramref name="length"/> meters along <paramref name="direction"/> from
        /// <paramref name="from"/>: the world's, a part of someone's body (<see cref="Hitbox"/>), or a raised guard's face
        /// before it (<see cref="GuardFace"/>); never <paramref name="archer"/>'s own, which the arrow leaves from beside,
        /// nor any other trigger.
        /// </summary>
        public static bool TryMeet(Vector3 from, Vector3 direction, float length, CharacterModel archer, out RaycastHit nearest)
        {
            nearest = default;
            var found = false;
            var own = archer != null ? archer.Root.transform : null;
            var count = Physics.RaycastNonAlloc(from, direction, Hits, length, SceneLayers.Strikes, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if ((found && hit.distance >= nearest.distance) || (hit.collider.isTrigger && !IsOnSomeone(hit.collider)) ||
                    (own != null && hit.collider.transform.IsChildOf(own)))
                    continue;
                nearest = hit;
                found = true;
            }

            return found;
        }

        /// <summary>Whether <paramref name="collider"/> is a raised guard's face (<see cref="GuardFace"/>).</summary>
        public static bool IsGuard(Collider collider) => collider.GetComponent<GuardFace>() != null;

        /// <summary>Whether <paramref name="collider"/> moves with someone, as part of them: a hitbox, or a raised guard's face.</summary>
        public static bool IsOnSomeone(Collider collider) => IsGuard(collider) || collider.GetComponent<Hitbox>() != null;
    }
}
