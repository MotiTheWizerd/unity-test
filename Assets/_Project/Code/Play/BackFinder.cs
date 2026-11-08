using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where a character carries a long thing on its back (a bow, as in Skyrim), read off the character itself rather than
    /// placed by hand: played in its idle on a hidden copy (<see cref="ClipSampler"/>), the line from one hip up across the
    /// back to the shoulder of the hand that holds it (a bow held in the left: right hip to left shoulder, so that hand
    /// reaches it over its own shoulder), its middle at the height of the chest bone (the upper back, below the neck) and
    /// laid on the back's outside (the farthest back the torso reaches, clothes and all, between the shoulders and the
    /// hips). Relative to the chest, so it moves with the torso. And when
    /// in a draw or a put-away the hand is at it (<see cref="TryFindReach"/>): the moment the thing changes place.
    /// Measured once per character, clip and hand, then remembered.
    /// </summary>
    public static class BackFinder
    {
        // Samples across a draw: a second's clip at 30 fps has 30 frames.
        const int Samples = 60;

        static readonly Dictionary<(GameObject, AnimationClip, bool), Pose?> Spots = new Dictionary<(GameObject, AnimationClip, bool), Pose?>();
        static readonly Dictionary<(GameObject, AnimationClip, AnimationClip, bool), float?> Reaches = new Dictionary<(GameObject, AnimationClip, AnimationClip, bool), float?>();

        /// <summary>
        /// The spot on <paramref name="character"/>'s back for what its right or left hand holds, as it stands in
        /// <paramref name="idle"/>, relative to its chest bone (<see cref="Chest"/>): +Y up the line to that hand's
        /// shoulder, +Z out of the back. False when it can't tell (no clip, no humanoid, no mesh).
        /// </summary>
        public static bool TryFind(GameObject character, AnimationClip idle, bool right, out Pose inChest)
        {
            inChest = default;
            if (character == null || idle == null)
                return false;
            var key = (character, idle, right);
            if (!Spots.TryGetValue(key, out var found))
            {
                found = Measure(character, idle, right);
                Spots.Add(key, found);
            }

            inChest = found ?? default;
            return found.HasValue;
        }

        /// <summary>
        /// How far into <paramref name="clip"/> (a draw or a put-away) the right or left hand comes nearest its spot on the
        /// back, 0 to 1: when what it holds goes from the back to the hand, or back. False when it can't tell.
        /// </summary>
        public static bool TryFindReach(GameObject character, AnimationClip clip, AnimationClip idle, bool right, out float moment)
        {
            moment = 1f;
            if (clip == null || !(clip.length > 0f) || !TryFind(character, idle, right, out var spot))
                return false;
            var key = (character, clip, idle, right);
            if (!Reaches.TryGetValue(key, out var found))
            {
                found = MeasureReach(character, clip, right, spot);
                Reaches.Add(key, found);
            }

            moment = found ?? 1f;
            return found.HasValue;
        }

        /// <summary>The bone a back spot rides on: the upper chest, else the chest, else the spine; null without one.</summary>
        public static Transform Chest(Animator humanoid)
        {
            if (humanoid == null)
                return null;
            var chest = humanoid.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null)
                chest = humanoid.GetBoneTransform(HumanBodyBones.Chest);
            return chest != null ? chest : humanoid.GetBoneTransform(HumanBodyBones.Spine);
        }

        static Pose? Measure(GameObject character, AnimationClip idle, bool right)
        {
            using (var sampler = ClipSampler.TryCreate(character, idle))
            {
                if (sampler == null)
                    return null;
                sampler.PoseAt(0f);
                var animator = sampler.Animator;
                var chest = Chest(animator);
                var hip = animator.GetBoneTransform(right ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                var shoulder = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
                var otherShoulder = animator.GetBoneTransform(right ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                if (chest == null || hip == null || shoulder == null || otherShoulder == null)
                    return null;

                // In the copy's frame, which faces +Z: the line across the back, seen from behind.
                var root = sampler.Root;
                var from = root.InverseTransformPoint(hip.position);
                var to = root.InverseTransformPoint(shoulder.position);
                var across = to - from;
                across.z = 0f;
                if (across.sqrMagnitude < 1e-6f || !(to.y > from.y))
                    return null;

                // Its middle on that line at the chest bone's height: the upper back, below the neck.
                var height = root.InverseTransformPoint(chest.position).y;
                var middle = Vector3.Lerp(from, to, Mathf.Clamp01((height - from.y) / (to.y - from.y)));

                // The torso between the shoulders and from the hips up to them; its farthest back is the back's outside.
                var halfWidth = Mathf.Abs(to.x - root.InverseTransformPoint(otherShoulder.position).x) * 0.5f;
                var back = float.PositiveInfinity;
                foreach (var point in sampler.MeshPoints())
                {
                    if (Mathf.Abs(point.x - middle.x) <= halfWidth && point.y >= from.y && point.y <= to.y)
                        back = Mathf.Min(back, point.z);
                }

                if (float.IsInfinity(back))
                    return null;
                var spot = new Pose(new Vector3(middle.x, middle.y, back), Quaternion.LookRotation(Vector3.back, across.normalized));
                var position = root.TransformPoint(spot.position);
                var rotation = root.rotation * spot.rotation;
                var inverse = Quaternion.Inverse(chest.rotation);
                return new Pose(inverse * (position - chest.position), inverse * rotation);
            }
        }

        static float? MeasureReach(GameObject character, AnimationClip clip, bool right, Pose spot)
        {
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                var chest = sampler != null ? Chest(sampler.Animator) : null;
                if (chest == null)
                    return null;
                var nearest = float.PositiveInfinity;
                var moment = 1f;
                for (var i = 0; i <= Samples; i++)
                {
                    var at = (float)i / Samples;
                    if (!sampler.TryGripAt(clip.length * at, right, out var grip))
                        return null;
                    var distance = Vector3.Distance(grip.position, chest.position + chest.rotation * spot.position);
                    if (distance < nearest)
                    {
                        nearest = distance;
                        moment = at;
                    }
                }

                return moment;
            }
        }
    }
}
