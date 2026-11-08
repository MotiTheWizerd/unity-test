using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Which way a character's body lies from each hand as it stands ready (<see cref="CharacterModel.StanceClip"/>), read
    /// off its own clip on a hidden copy (<see cref="ClipSampler"/>) rather than set by hand: from the hand's grip to its
    /// chest, in the grip's frame. A thing with a side that must face whoever holds it (a bow's string, for the other hand
    /// to take) is turned in the hand, about its length, to face it (<see cref="Facing"/>). Measured once per character,
    /// clip and hand, then remembered.
    /// </summary>
    public static class HoldFinder
    {
        static readonly Dictionary<(GameObject, AnimationClip, bool), Vector3?> Found = new Dictionary<(GameObject, AnimationClip, bool), Vector3?>();

        /// <summary>
        /// The way from <paramref name="character"/>'s right or left grip to its chest as it stands at the start of
        /// <paramref name="stance"/>, in the grip's frame (a unit vector); false when it can't tell.
        /// </summary>
        public static bool TryFindBody(GameObject character, AnimationClip stance, bool right, out Vector3 inGrip)
        {
            inGrip = default;
            if (character == null || stance == null)
                return false;
            var key = (character, stance, right);
            if (!Found.TryGetValue(key, out var found))
            {
                found = Measure(character, stance, right);
                Found.Add(key, found);
            }

            inGrip = found ?? default;
            return found.HasValue;
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

        static Vector3? Measure(GameObject character, AnimationClip stance, bool right)
        {
            using (var sampler = ClipSampler.TryCreate(character, stance))
            {
                if (sampler == null || !sampler.TryGripAt(0f, right, out var grip))
                    return null;
                var chest = sampler.Animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (chest == null)
                    chest = sampler.Animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest == null)
                    return null;
                var toward = Quaternion.Inverse(grip.rotation) * (chest.position - grip.position);
                return toward.sqrMagnitude > 1e-8f ? toward.normalized : (Vector3?)null;
            }
        }
    }
}
