using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where a hand holds what it grips, worked out from a humanoid's bones rather than tuned per rig, so any character
    /// (the Viking, an NPC from Mixamo) holds a sword the same way: the hand, a knuckle (the fingers' way) and the thumb
    /// (the side the blade leaves the fist by). The grip sits in the palm, a little past the knuckles and in from them;
    /// what is held points its +Y out of the fist past the thumb, faces its +Z out of the palm, and lays its +X along the
    /// fingers, so a blade modelled up +Y with its guard across X is held edge forward, flats to the palm. The palm is
    /// found by the hand's side: the left is the right in a mirror. Pure math over positions, so it is testable.
    /// </summary>
    public static class GripFrame
    {
        /// <summary>How far along the fingers the grip sits, in hand-to-knuckle lengths: across the fingers' bases.</summary>
        public const float Along = 1f;

        /// <summary>
        /// How far into the palm the grip sits, in hand-to-knuckle lengths: a handle's thickness off the palm, inside the
        /// curled fingers (Mixamo's hands curl toward a fist, their fingertips about half a knuckle length off the palm).
        /// </summary>
        public const float Into = 0.25f;

        /// <summary>
        /// The grip of a hand (scene space): <paramref name="hand"/> the hand bone, <paramref name="knuckle"/> a finger's
        /// first bone, <paramref name="thumb"/> the thumb's first bone.
        /// </summary>
        public static Pose Hand(Vector3 hand, Vector3 knuckle, Vector3 thumb, bool right)
        {
            var reach = knuckle - hand;
            var length = reach.magnitude;
            var fingers = reach / length;
            var thumbSide = Vector3.ProjectOnPlane(thumb - hand, fingers).normalized;
            var palm = Vector3.Cross(fingers, thumbSide) * (right ? 1f : -1f);
            var position = hand + fingers * (length * Along) + palm * (length * Into);
            return new Pose(position, Quaternion.LookRotation(palm, thumbSide));
        }
    }
}
