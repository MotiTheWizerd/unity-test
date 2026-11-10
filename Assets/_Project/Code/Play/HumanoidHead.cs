using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A humanoid's head, read off the character itself rather than set by hand: its bones (the head and everything under
    /// it: jaw, eyes, a helmet's), which first person folds away (<see cref="HeadlessBody"/>), how high its eyes are
    /// (<see cref="TryFindEyeLevel"/>) and where (<see cref="TryFindEyes"/>), which the first-person camera rides. Each is
    /// measured once per character and clip (and height), then remembered.
    /// </summary>
    public static class HumanoidHead
    {
        // How far up the head the eyes are, from its joint (0) to its top (1). On the Mixamo-rigged characters (VikingHero,
        // VikingM1, VikingF1, ElfF1) they sit halfway, where their faces put them too (just above the nose's tip) within 1 cm.
        const float EyesUpTheHead = 0.5f;

        static readonly Dictionary<(GameObject, AnimationClip, float), Vector3?> Found = new Dictionary<(GameObject, AnimationClip, float), Vector3?>();
        static readonly Dictionary<(GameObject, AnimationClip), float?> Levels = new Dictionary<(GameObject, AnimationClip), float?>();

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
        /// How high above its root <paramref name="character"/>'s eyes are as it stands at the start of
        /// <paramref name="clip"/> (its idle): halfway from its head joint to the top of what its head carries (hair, a
        /// helmet raise it a little). False when it has no head to find them on.
        /// </summary>
        public static bool TryFindEyeLevel(GameObject character, AnimationClip clip, out float aboveRoot)
        {
            aboveRoot = 0f;
            if (character == null || clip == null)
                return false;
            var key = (character, clip);
            if (!Levels.TryGetValue(key, out var found))
            {
                found = MeasureLevel(character, clip);
                Levels.Add(key, found);
            }

            aboveRoot = found ?? 0f;
            return found.HasValue;
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

        static float? MeasureLevel(GameObject character, AnimationClip clip)
        {
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                var head = sampler?.Animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null)
                    return null;
                sampler.PoseAt(0f);

                var joint = sampler.Root.InverseTransformPoint(head.position).y;
                var top = float.NegativeInfinity;
                foreach (var point in sampler.MeshPoints(Bones(sampler.Animator)))
                    top = Mathf.Max(top, point.y);

                // A head the mesh doesn't show has no top to go by.
                return top > joint ? joint + (top - joint) * EyesUpTheHead : (float?)null;
            }
        }
    }
}
