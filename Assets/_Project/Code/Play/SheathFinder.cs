using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Where a character's sheaths are, read off its own draw (or sheathe) clip rather than placed by hand: played on a
    /// hidden copy of the character (<see cref="ClipSampler"/>), the moment the right hand comes nearest the left hip is when it takes the hilt, and
    /// where its grip is then is where the hilt hangs, relative to the hips (so it moves with them). The blade points back
    /// the way the hand pulls it out, and the sheath stands just outside the body there (<see cref="Hang"/>). The left
    /// hand's sheath is that one in a mirror (the right hip). Measured once per character and clip, then remembered.
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

        /// <summary>How far a scabbard stands out from the body beside it, meters (half its thickness and a little air).</summary>
        public const float Scabbard = 0.03f;

        // Seconds after the grab the hand's way is read over: long enough to see where it pulls, short enough to be the pull.
        const float PullSeconds = 0.15f;

        // A hand moving less than this (meters) is not pulling anywhere.
        const float MinPull = 0.01f;

        // Along the blade, how often (meters) and how near beside it (meters) the body is looked for.
        const float BodyStep = 0.05f;
        const float BodyReach = 0.05f;

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
            using (var sampler = ClipSampler.TryCreate(character, clip))
            {
                if (sampler == null)
                    return null;
                var animator = sampler.Animator;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var hip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                if (hips == null || hip == null || knee == null)
                    return null;

                var best = float.MaxValue;
                var moment = 0f;
                for (var i = 0; i < Samples; i++)
                {
                    var t = (float)i / (Samples - 1);
                    if (!sampler.TryGripAt(t * clip.length, true, out var now))
                        return null;
                    var distance = Vector3.Distance(now.position, hip.position);
                    if (distance < best)
                    {
                        best = distance;
                        moment = t;
                    }
                }

                // Where the hand goes just after the grab is the way the blade comes out of the scabbard; then back to
                // the grab, to measure the body around it. All in the character's own frame (it faces +Z).
                var root = sampler.Root;
                if (!sampler.TryGripAt(moment * clip.length + PullSeconds, true, out var pulled) ||
                    !sampler.TryGripAt(moment * clip.length, true, out var grip))
                    return null;
                var right = Hang(Local(root, grip), root.InverseTransformPoint(pulled.position), BodyPoints(root.gameObject, root),
                    Vector3.Distance(hip.position, knee.position), Vector3.left);

                // The left sheath is the right one across the character's middle.
                var left = new Pose(Vector3.Scale(right.position, new Vector3(-1f, 1f, 1f)),
                    new Quaternion(right.rotation.x, -right.rotation.y, -right.rotation.z, right.rotation.w));
                var hipsLocal = Local(root, new Pose(hips.position, hips.rotation));
                return new Sheaths(moment, Relative(hipsLocal, right), Relative(hipsLocal, left));
            }
        }

        /// <summary>
        /// How a sheathed blade hangs, from the hand's grip as it takes the hilt (<paramref name="grip"/>) and where the
        /// hand has gone a moment later (<paramref name="pulledTo"/>): the blade (+Y) points back the way it was pulled
        /// out, flats (+Z) to the <paramref name="outward"/> side, and the whole sheath stands out that way past every
        /// point of <paramref name="body"/> beside the blade's first <paramref name="length"/> meters, by
        /// <see cref="Scabbard"/>. Without a pull to go by, the blade keeps the grip's own +Y. Pure math, so testable.
        /// </summary>
        public static Pose Hang(Pose grip, Vector3 pulledTo, IReadOnlyList<Vector3> body, float length, Vector3 outward)
        {
            var pull = pulledTo - grip.position;
            var blade = pull.magnitude >= MinPull ? -pull.normalized : grip.rotation * Vector3.up;
            outward = outward.normalized;

            var push = 0f;
            for (var along = 0f; along <= length; along += BodyStep)
            {
                var point = grip.position + blade * along;
                foreach (var vertex in body)
                {
                    var offset = vertex - point;
                    var standsOut = Vector3.Dot(offset, outward);
                    if ((offset - outward * standsOut).sqrMagnitude < BodyReach * BodyReach)
                        push = Mathf.Max(push, standsOut + Scabbard);
                }
            }

            var flats = Vector3.ProjectOnPlane(outward, blade).normalized;
            return new Pose(grip.position + outward * push, Quaternion.LookRotation(flats, blade));
        }

        // Every vertex of the character as it stands now, skinned meshes posed, in root's frame.
        static List<Vector3> BodyPoints(GameObject character, Transform root)
        {
            var points = new List<Vector3>();
            var baked = new Mesh();
            try
            {
                foreach (var skinned in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skinned.BakeMesh(baked, false);
                    AddPoints(points, baked, skinned.transform, root);
                }

                foreach (var filter in character.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null && filter.sharedMesh.isReadable)
                        AddPoints(points, filter.sharedMesh, filter.transform, root);
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }

            return points;
        }

        static void AddPoints(List<Vector3> points, Mesh mesh, Transform from, Transform root)
        {
            foreach (var vertex in mesh.vertices)
                points.Add(root.InverseTransformPoint(from.TransformPoint(vertex)));
        }

        static Pose Local(Transform root, Pose pose) =>
            new Pose(root.InverseTransformPoint(pose.position), Quaternion.Inverse(root.rotation) * pose.rotation);

        // pose seen from frame (both in the same space).
        static Pose Relative(Pose frame, Pose pose)
        {
            var inverse = Quaternion.Inverse(frame.rotation);
            return new Pose(inverse * (pose.position - frame.position), inverse * pose.rotation);
        }
    }
}
