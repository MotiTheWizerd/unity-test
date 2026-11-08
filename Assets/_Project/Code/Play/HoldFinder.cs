using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// How a thing with a side that must face whoever holds it (a bow's string, for the other hand to take) is turned
    /// about its length to face a way: in the hand towards the chest, as the hand is posed each frame
    /// (<see cref="CharacterModel"/>); on the back, down across it.
    /// </summary>
    public static class HoldFinder
    {
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
