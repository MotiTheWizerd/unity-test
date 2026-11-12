using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Which way a clip turns the body's hips from where its root faces, moment by moment, read off the clip itself as
    /// <see cref="RaiseFinder"/> reads a block (<see cref="ClipSampler"/>): the hips' yaw is the way square to the line
    /// across the upper legs (<see cref="HipsYaw"/>). Mixamo's clips disagree: Sword And Shield Idle, Block and Strafe
    /// stand side-on, the hips some 55° to the right with the shield leading, while its Walk and Run go square-on. An
    /// action on the upper body (<see cref="HumanoidAnimation.UpperBodyLayer"/>) sets the spine on whatever hips the legs
    /// have, so over a walk a raised shield would turn as far aside; knowing the stance it was made in
    /// (<see cref="TryFind"/>) and the hips it stands on now (<see cref="TryMeasure"/>), the look turns its upper body
    /// back by the difference (<see cref="Keep"/>, <see cref="CharacterModel"/>). Measured once per character and clip,
    /// then remembered.
    /// </summary>
    public static class StanceFinder
    {
        // Samples across the clip: a 1 s clip at 30 fps has 31 frames.
        const int Samples = 31;

        static readonly Dictionary<(GameObject, AnimationClip), float[]> Found = new Dictionary<(GameObject, AnimationClip), float[]>();

        /// <summary>
        /// How far <paramref name="clip"/> turns <paramref name="character"/>'s hips from where its root faces,
        /// <paramref name="at"/> (a fraction of the clip), degrees, + to the right; false when it can't tell (no clip, no legs).
        /// </summary>
        public static bool TryFind(GameObject character, AnimationClip clip, float at, out float yaw)
        {
            yaw = 0f;
            if (character == null || clip == null)
                return false;
            var key = (character, clip);
            if (!Found.TryGetValue(key, out var yaws))
            {
                yaws = Measure(character, clip);
                Found.Add(key, yaws);
            }

            if (yaws == null)
                return false;
            yaw = At(yaws, at);
            return true;
        }

        /// <summary>How far the posed <paramref name="humanoid"/>'s hips are turned from where <paramref name="root"/> faces, degrees, + to the right; false without legs.</summary>
        public static bool TryMeasure(Animator humanoid, Transform root, out float yaw)
        {
            yaw = 0f;
            var left = humanoid != null ? humanoid.GetBoneTransform(HumanBodyBones.LeftUpperLeg) : null;
            var right = humanoid != null ? humanoid.GetBoneTransform(HumanBodyBones.RightUpperLeg) : null;
            if (left == null || right == null || root == null)
                return false;
            yaw = HipsYaw(left.position, right.position, root.forward, root.up);
            return true;
        }

        /// <summary>
        /// Which way hips at <paramref name="leftHip"/> and <paramref name="rightHip"/> face, square to the line across
        /// them, from <paramref name="forward"/>, level about <paramref name="up"/>: degrees, + to the right. Pure, so testable.
        /// </summary>
        public static float HipsYaw(Vector3 leftHip, Vector3 rightHip, Vector3 forward, Vector3 up)
        {
            var facing = Vector3.ProjectOnPlane(Vector3.Cross(rightHip - leftHip, up), up);
            var ahead = Vector3.ProjectOnPlane(forward, up);
            return facing.sqrMagnitude < 1e-10f || ahead.sqrMagnitude < 1e-10f ? 0f : Vector3.SignedAngle(ahead, facing, up);
        }

        /// <summary>
        /// The yaw <paramref name="at"/> (a fraction of the clip) among <paramref name="yaws"/>, evenly spaced from its start
        /// to its end, between the two nearest. Pure, so testable.
        /// </summary>
        public static float At(IReadOnlyList<float> yaws, float at)
        {
            if (yaws == null || yaws.Count == 0)
                throw new ArgumentException("No yaws to read.", nameof(yaws));
            if (yaws.Count == 1)
                return yaws[0];
            var place = Mathf.Clamp01(at) * (yaws.Count - 1);
            var below = Mathf.Min((int)place, yaws.Count - 2);
            return yaws[below] + Mathf.DeltaAngle(yaws[below], yaws[below + 1]) * (place - below);
        }

        /// <summary>
        /// How far to turn an upper body set on hips turned <paramref name="now"/> so that it stands as on the hips it was
        /// made on (<paramref name="made"/>), shown <paramref name="weight"/> (0 to 1) of the way, a twist of at most
        /// <paramref name="most"/> degrees either way: degrees, + to the right. Pure, so testable.
        /// </summary>
        public static float Keep(float made, float now, float weight, float most) =>
            Mathf.Clamp(Mathf.DeltaAngle(now, made), -most, most) * Mathf.Clamp01(weight);

        static float[] Measure(GameObject character, AnimationClip clip)
        {
            if (!(clip.length > 0f))
                return null;
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                if (sampler == null)
                    return null;
                var yaws = new float[Samples];
                for (var i = 0; i < Samples; i++)
                {
                    sampler.PoseAt(clip.length * i / (Samples - 1));
                    if (!TryMeasure(sampler.Animator, sampler.Root, out yaws[i]))
                        return null;
                }

                return yaws;
            }
        }
    }
}
