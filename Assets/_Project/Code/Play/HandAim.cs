using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Turns a hand so what it holds points where its move wants it, the least it needs to. A Mixamo move is animated
    /// with nothing in the hand, so a sword held by our grip (<see cref="GripFrame"/>) may end up across the body where
    /// the move meant it ahead; the move's data says where (a clip's .json: where the grip's +Y points, in the body's
    /// frame), the animator blends how much (the curves it writes), and this says how: the one rotation that takes
    /// the held thing's way to the wanted one, scaled by how much. On top of the clip's pose, so the fingers, the grip
    /// and what it holds all come along. Pure math over directions, so it is testable.
    /// </summary>
    public static class HandAim
    {
        /// <summary>
        /// The least turn that takes <paramref name="held"/> (where the held thing points now) to <paramref name="wanted"/>,
        /// by as much of it as <paramref name="wanted"/>'s length says (0: not at all, 1 or more: all the way); none when
        /// either is nothing.
        /// </summary>
        public static Quaternion Turn(Vector3 held, Vector3 wanted)
        {
            var weight = Mathf.Clamp01(wanted.magnitude);
            if (weight <= 0f || held == Vector3.zero)
                return Quaternion.identity;
            var whole = Quaternion.FromToRotation(held, wanted);
            return weight >= 1f ? whole : Quaternion.Slerp(Quaternion.identity, whole, weight);
        }
    }
}
