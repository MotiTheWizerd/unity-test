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
            var turn = weight >= 1f ? whole : Quaternion.Slerp(Quaternion.identity, whole, weight);
            // No further than an arm turns a hand: past it the hand goes as far as it can toward the wanted way.
            var angle = Quaternion.Angle(Quaternion.identity, turn);
            return angle <= MaxTurn ? turn : Quaternion.Slerp(Quaternion.identity, turn, MaxTurn / angle);
        }

        /// <summary>
        /// Degrees a hand is turned at most, its forearm's roll and its wrist's bend together: past about this a wrist
        /// reads as broken, however the turn is shared. Holding a thing further round than this is the arm's work, not
        /// the hand's.
        /// </summary>
        public const float MaxTurn = 60f;

        /// <summary>
        /// How much of a hand's roll the forearm takes: a hand rolls by turning the forearm (the wrist only bends), and a
        /// rig without twist bones shows a roll where it is put, so it is shared between the elbow and the wrist rather
        /// than all wrung at one.
        /// </summary>
        public const float ForearmShare = 0.5f;

        /// <summary>
        /// The forearm's share of a hand's <paramref name="turn"/>: <see cref="ForearmShare"/> of the turn's roll about
        /// <paramref name="forearm"/> (its length, elbow to wrist), which leaves the wrist where it is. Applied to the
        /// forearm first, the hand then takes <c>turn * Inverse(share)</c>, and together they make the whole turn.
        /// </summary>
        public static Quaternion ForearmPart(Quaternion turn, Vector3 forearm) =>
            Quaternion.Slerp(Quaternion.identity, TwistAbout(turn, forearm), ForearmShare);

        /// <summary>The part of <paramref name="turn"/> that rolls about <paramref name="axis"/> (its twist); none about no axis.</summary>
        public static Quaternion TwistAbout(Quaternion turn, Vector3 axis)
        {
            if (axis == Vector3.zero)
                return Quaternion.identity;
            var along = Vector3.Project(new Vector3(turn.x, turn.y, turn.z), axis.normalized);
            var twist = new Vector4(along.x, along.y, along.z, turn.w);
            var length = twist.magnitude;
            // A half turn exactly across the axis has no roll about it.
            return length < 1e-6f ? Quaternion.identity : new Quaternion(twist.x / length, twist.y / length, twist.z / length, twist.w / length);
        }
    }
}
