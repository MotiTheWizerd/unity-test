using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// How a thing with a side that must face whoever holds it (a bow's string, for the other hand to take) is turned
    /// about its length to face a way: in the hand towards the chest, as the hand is posed each frame
    /// (<see cref="CharacterModel"/>); on the back, down across it. A thing strapped to the forearm (a shield) is
    /// turned once, to lie along the arm with that side to the hand (<see cref="Strapped"/>).
    /// </summary>
    public static class HoldFinder
    {
        /// <summary>
        /// The turn in a hand's grip (<see cref="GripFrame"/>) that straps a thing to the forearm, as a shield is held:
        /// <paramref name="facesHand"/> (in the thing's frame: a shield's back) faces the hand, out of the palm, and
        /// <paramref name="alongArm"/> (from the grip towards its middle) runs up the forearm, the way the fingers don't.
        /// The grip of the <paramref name="right"/> hand lays its +X along the fingers and the left's back along them (the
        /// left is the right in a mirror), so a thing is strapped to either arm the same way round. None when the two lie
        /// along each other.
        /// </summary>
        public static Quaternion Strapped(Vector3 facesHand, Vector3 alongArm, bool right)
        {
            if (Vector3.Cross(facesHand, alongArm).sqrMagnitude < 1e-8f)
                return Quaternion.identity;
            var upTheArm = right ? Vector3.left : Vector3.right;
            return Quaternion.LookRotation(Vector3.forward, upTheArm) * Quaternion.Inverse(Quaternion.LookRotation(facesHand, alongArm));
        }

        /// <summary>
        /// The turn, in a place whose +Z stands out of the body (the back), that hangs a strapped thing flat against it as
        /// a shield is slung: <paramref name="facesBody"/> (in the thing's frame: a shield's back) against the body, and
        /// its +Y (up out of the fist that held it: a kite shield's top) towards <paramref name="up"/>, seen across the
        /// body. None when either lies along the other's way.
        /// </summary>
        public static Quaternion Against(Vector3 facesBody, Vector3 up)
        {
            up.z = 0f;
            if (up.sqrMagnitude < 1e-8f || Vector3.Cross(facesBody, Vector3.up).sqrMagnitude < 1e-8f)
                return Quaternion.identity;
            return Quaternion.LookRotation(Vector3.back, up) * Quaternion.Inverse(Quaternion.LookRotation(facesBody, Vector3.up));
        }

        /// <summary>
        /// The turn about a grip's length (+Y) that brings <paramref name="side"/> (in the grip's frame) round to face
        /// <paramref name="toward"/>, both seen across the length; none when either lies along it.
        /// </summary>
        public static Quaternion Facing(Vector3 side, Vector3 toward)
        {
            side.y = 0f;
            toward.y = 0f;
            if (side.sqrMagnitude < 1e-8f || toward.sqrMagnitude < 1e-8f)
                return Quaternion.identity;
            return Quaternion.AngleAxis(Vector3.SignedAngle(side, toward, Vector3.up), Vector3.up);
        }
    }
}
