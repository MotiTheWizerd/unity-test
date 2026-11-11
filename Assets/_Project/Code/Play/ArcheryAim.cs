using Forge.Game.Cameras;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where an archer's arrow lies and where it is aimed, shared by the arrow on the string (<see cref="ShotView"/>)
    /// and the arrow in flight (<see cref="ArrowFlights"/>): it lies along the draw, from the drawing (right) hand to the
    /// bow (left) hand; it is aimed, as in Skyrim, at what the centre of the view meets. Scene coordinates.
    /// </summary>
    public static class ArcheryAim
    {
        // Meters the view's line is followed to find what it points at.
        const float Reach = 1000f;

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
        /// What <paramref name="view"/>'s line meets first (the scene's colliders, triggers ignored), else as far along it
        /// as it is followed; when that is behind <paramref name="from"/> (a wall at the shoulder), straight along the view
        /// from there.
        /// </summary>
        public static Vector3 Target(ViewPose view, Vector3 from)
        {
            var origin = view.Position.ToUnity();
            var forward = view.Forward.ToUnity();
            var target = Physics.Raycast(origin, forward, out var hit, Reach, SceneLayers.World, QueryTriggerInteraction.Ignore)
                ? hit.point
                : origin + forward * Reach;
            return Vector3.Dot(target - from, forward) > 0f ? target : from + forward * Reach;
        }
    }
}
