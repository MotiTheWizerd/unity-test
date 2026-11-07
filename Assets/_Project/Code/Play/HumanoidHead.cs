using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A humanoid's head, read off the character itself rather than set by hand: its bones (the head and everything under
    /// it: jaw, eyes, a helmet's), which first person folds away (<see cref="HeadlessBody"/>), and where its eyes are
    /// (<see cref="TryFindEyes"/>), which the first-person camera rides. Eyes are measured once per character, clip and
    /// height, then remembered.
    /// </summary>
    public static class HumanoidHead
    {
        static readonly Dictionary<(GameObject, AnimationClip, float), Vector3?> Found = new Dictionary<(GameObject, AnimationClip, float), Vector3?>();

        /// <summary>The head bone and every bone under it; empty when <paramref name="humanoid"/> has no head.</summary>
        public static HashSet<Transform> Bones(Animator humanoid)
        {
            var bones = new HashSet<Transform>();
            var head = humanoid != null && humanoid.isHuman ? humanoid.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null)
            {
                foreach (var bone in head.GetComponentsInChildren<Transform>(true))
                    bones.Add(bone);
            }

            return bones;
        }

        /// <summary>
        /// Where <paramref name="character"/>'s eyes are, in its head bone's own frame: <paramref name="height"/> above its
        /// root as it stands at the start of <paramref name="clip"/> (its idle), at the front of its face (the farthest ahead
        /// of what its head carries) and midway across it. False when it has no head to find them on.
        /// </summary>
        public static bool TryFindEyes(GameObject character, AnimationClip clip, float height, out Vector3 inHead)
        {
            inHead = default;
            if (character == null || clip == null)
                return false;
            var key = (character, clip, height);
            if (!Found.TryGetValue(key, out var found))
            {
                found = Measure(character, clip, height);
                Found.Add(key, found);
            }

            inHead = found ?? default;
            return found.HasValue;
        }

        static Vector3? Measure(GameObject character, AnimationClip clip, float height)
        {
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                var head = sampler?.Animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null)
                    return null;
                sampler.PoseAt(0f);

                // The face's front and its middle; a head the mesh doesn't show (no skin on its bones) has them at the bone.
                var pivot = sampler.Root.InverseTransformPoint(head.position);
                float left = float.PositiveInfinity, right = float.NegativeInfinity, front = float.NegativeInfinity;
                foreach (var point in sampler.MeshPoints(Bones(sampler.Animator)))
                {
                    left = Mathf.Min(left, point.x);
                    right = Mathf.Max(right, point.x);
                    front = Mathf.Max(front, point.z);
                }

                var eyes = float.IsInfinity(front) ? new Vector3(pivot.x, height, pivot.z) : new Vector3((left + right) * 0.5f, height, front);
                return head.InverseTransformPoint(sampler.Root.TransformPoint(eyes));
            }
        }
    }
}
