using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Forge.Play
{
    /// <summary>
    /// Where a character's sheaths are, read off its own draw (or sheathe) clip rather than placed by hand: played on a
    /// hidden copy of the character, the moment the right hand comes nearest the left hip is when it takes the hilt, and
    /// where its grip is then is where the sword hangs, relative to the hips (so it moves with them). The left hand's
    /// sheath is that one in a mirror (the right hip). Measured once per character and clip, then remembered.
    /// </summary>
    public static class SheathFinder
    {
        /// <summary>Where the sheaths are on one character, and when in the clip the hand is at them.</summary>
        public readonly struct Sheaths
        {
            public Sheaths(float moment, Pose right, Pose left)
            {
                Moment = moment;
                Right = right;
                Left = left;
            }

            /// <summary>How far into the clip the hand is at the sheath, 0 to 1.</summary>
            public float Moment { get; }

            /// <summary>The right hand's sheath (on the left hip), relative to the hips.</summary>
            public Pose Right { get; }

            /// <summary>The left hand's sheath (on the right hip), relative to the hips.</summary>
            public Pose Left { get; }

            /// <summary>The sheath of the right or the left hand.</summary>
            public Pose Of(bool right) => right ? Right : Left;
        }

        // Samples across the clip: a 1.5 s clip at 30 fps has 45 frames.
        const int Samples = 60;

        static readonly Dictionary<(GameObject, AnimationClip), Sheaths?> Found = new Dictionary<(GameObject, AnimationClip), Sheaths?>();

        /// <summary>Where <paramref name="character"/>'s sheaths are by <paramref name="clip"/>; false when it can't tell (no clip, no hands).</summary>
        public static bool TryFind(GameObject character, AnimationClip clip, out Sheaths sheaths)
        {
            sheaths = default;
            if (character == null || clip == null)
                return false;
            var key = (character, clip);
            if (!Found.TryGetValue(key, out var found))
            {
                found = Measure(character, clip);
                Found.Add(key, found);
            }

            if (found == null)
                return false;
            sheaths = found.Value;
            return true;
        }

        static Sheaths? Measure(GameObject character, AnimationClip clip)
        {
            var copy = Object.Instantiate(character, new Vector3(0f, -1000f, 0f), Quaternion.identity);
            var graph = PlayableGraph.Create("Sheath finder");
            try
            {
                foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                    renderer.forceRenderingOff = true;
                foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                var animator = copy.GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman)
                    return null;
                // Out of every camera's sight, it would be culled and never take a pose.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var hip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                if (hips == null || hip == null)
                    return null;

                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);

                var best = float.MaxValue;
                var moment = 0f;
                Pose grip = default;
                Pose hipsPose = default;
                for (var i = 0; i < Samples; i++)
                {
                    var t = (float)i / (Samples - 1);
                    playable.SetTime(t * clip.length);
                    graph.Evaluate();
                    if (!HumanoidHands.TryGrip(animator, true, out var now))
                        return null;
                    var distance = Vector3.Distance(now.position, hip.position);
                    if (distance < best)
                    {
                        best = distance;
                        moment = t;
                        grip = now;
                        hipsPose = new Pose(hips.position, hips.rotation);
                    }
                }

                // In the character's own frame (it faces +Z), the left sheath is the right one across its middle.
                var root = copy.transform;
                var right = new Pose(root.InverseTransformPoint(grip.position), Quaternion.Inverse(root.rotation) * grip.rotation);
                var left = new Pose(Vector3.Scale(right.position, new Vector3(-1f, 1f, 1f)),
                    new Quaternion(right.rotation.x, -right.rotation.y, -right.rotation.z, right.rotation.w));
                var hipsLocal = new Pose(root.InverseTransformPoint(hipsPose.position), Quaternion.Inverse(root.rotation) * hipsPose.rotation);
                return new Sheaths(moment, Relative(hipsLocal, right), Relative(hipsLocal, left));
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(copy);
            }
        }

        // pose seen from frame (both in the same space).
        static Pose Relative(Pose frame, Pose pose)
        {
            var inverse = Quaternion.Inverse(frame.rotation);
            return new Pose(inverse * (pose.position - frame.position), inverse * pose.rotation);
        }
    }
}
