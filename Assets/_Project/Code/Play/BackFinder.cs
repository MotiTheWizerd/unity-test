using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where a character carries a long thing on its back (a bow, as in Skyrim), read off the character itself rather than
    /// placed by hand: played in its idle on a hidden copy (<see cref="ClipSampler"/>), the line from its left hip to its
    /// right shoulder, across the back, its middle laid on the back's outside (the farthest back the torso reaches,
    /// clothes and all, between the shoulders and the hips). Relative to the chest, so it moves with the torso. Measured
    /// once per character and clip, then remembered.
    /// </summary>
    public static class BackFinder
    {
        static readonly Dictionary<(GameObject, AnimationClip), Pose?> Found = new Dictionary<(GameObject, AnimationClip), Pose?>();

        /// <summary>
        /// The spot on <paramref name="character"/>'s back as it stands in <paramref name="idle"/>, relative to its chest
        /// bone (<see cref="Chest"/>): +Y up the line from the left hip to the right shoulder, +Z out of the back. False
        /// when it can't tell (no clip, no humanoid, no mesh).
        /// </summary>
        public static bool TryFind(GameObject character, AnimationClip idle, out Pose inChest)
        {
            inChest = default;
            if (character == null || idle == null)
                return false;
            var key = (character, idle);
            if (!Found.TryGetValue(key, out var found))
            {
                found = Measure(character, idle);
                Found.Add(key, found);
            }

            inChest = found ?? default;
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

        static Pose? Measure(GameObject character, AnimationClip idle)
        {
            using (var sampler = ClipSampler.TryCreate(character, idle))
            {
                if (sampler == null)
                    return null;
                sampler.PoseAt(0f);
                var animator = sampler.Animator;
                var chest = Chest(animator);
                var hip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var otherShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                if (chest == null || hip == null || shoulder == null || otherShoulder == null)
                    return null;

                // In the copy's frame, which faces +Z: the line across the back, seen from behind.
                var root = sampler.Root;
                var from = root.InverseTransformPoint(hip.position);
                var to = root.InverseTransformPoint(shoulder.position);
                var across = to - from;
                across.z = 0f;
                if (across.sqrMagnitude < 1e-6f)
                    return null;
                var middle = (from + to) * 0.5f;

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
    }
}
