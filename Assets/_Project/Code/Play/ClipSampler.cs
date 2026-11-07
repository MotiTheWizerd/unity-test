using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Forge.Play
{
    /// <summary>
    /// A character's clip played on a hidden copy of it, posed at any moment asked for (<see cref="PoseAt"/>), so what a
    /// clip does (where the hand takes the hilt, when a blow is fastest) is measured from the clip itself rather than
    /// tuned by hand. The copy stands far below the world, drawn by no camera and colliding with nothing, and goes when
    /// the sampler is disposed. Null from <see cref="TryCreate"/> when the character has no humanoid to pose.
    /// </summary>
    public sealed class ClipSampler : IDisposable
    {
        readonly GameObject _copy;
        readonly PlayableGraph _graph;
        readonly AnimationClipPlayable _playable;

        ClipSampler(GameObject copy, Animator animator, AnimationClip clip)
        {
            _copy = copy;
            Animator = animator;
            Clip = clip;
            _graph = PlayableGraph.Create("Clip sampler");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _playable = AnimationClipPlayable.Create(_graph, clip);
            _playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(_graph, "Pose", animator).SetSourcePlayable(_playable);
        }

        /// <summary>The copy's humanoid, posed as the last <see cref="PoseAt"/> left it.</summary>
        public Animator Animator { get; }

        /// <summary>The copy itself (its root faces +Z, as the character does).</summary>
        public Transform Root => _copy.transform;

        public AnimationClip Clip { get; }

        /// <summary>A sampler of <paramref name="clip"/> on <paramref name="character"/>; null when it has no humanoid.</summary>
        public static ClipSampler TryCreate(GameObject character, AnimationClip clip)
        {
            if (character == null || clip == null)
                return null;
            var copy = Object.Instantiate(character, new Vector3(0f, -1000f, 0f), Quaternion.identity);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                renderer.forceRenderingOff = true;
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            var animator = copy.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Object.DestroyImmediate(copy);
                return null;
            }

            // Out of every camera's sight, it would be culled and never take a pose.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return new ClipSampler(copy, animator, clip);
        }

        /// <summary>Poses the copy <paramref name="seconds"/> into the clip.</summary>
        public void PoseAt(float seconds)
        {
            _playable.SetTime(Mathf.Clamp(seconds, 0f, Clip.length));
            _graph.Evaluate();
        }

        /// <summary>Where the right or left hand grips <paramref name="seconds"/> into the clip (scene space); false when the rig lacks the hand's bones.</summary>
        public bool TryGripAt(float seconds, bool right, out Pose grip)
        {
            PoseAt(seconds);
            return HumanoidHands.TryGrip(Animator, right, out grip);
        }

        /// <summary>Every vertex of the copy as it is posed now (skinned meshes baked), in its root's frame.</summary>
        public List<Vector3> MeshPoints() => MeshPoints(null);

        /// <summary>
        /// The vertices of the copy, as it is posed now, that <paramref name="bones"/> (the copy's own) carry: half their
        /// skinning weight or more, or the whole of a mesh that hangs from one of them; in its root's frame. Null bones: every vertex.
        /// </summary>
        public List<Vector3> MeshPoints(ICollection<Transform> bones)
        {
            var points = new List<Vector3>();
            var baked = new Mesh();
            try
            {
                foreach (var skinned in _copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var carried = bones == null ? null : Carried(skinned, bones);
                    if (carried != null && carried.Count == 0)
                        continue;
                    skinned.BakeMesh(baked, false);
                    AddPoints(points, baked, skinned.transform, carried);
                }

                foreach (var filter in _copy.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null && filter.sharedMesh.isReadable && (bones == null || filter.transform.HangsFrom(bones)))
                        AddPoints(points, filter.sharedMesh, filter.transform, null);
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }

            return points;
        }

        void AddPoints(List<Vector3> points, Mesh mesh, Transform from, HashSet<int> only)
        {
            var vertices = mesh.vertices;
            for (var i = 0; i < vertices.Length; i++)
            {
                if (only == null || only.Contains(i))
                    points.Add(Root.InverseTransformPoint(from.TransformPoint(vertices[i])));
            }
        }

        // Which of a skinned mesh's vertices the bones carry, by half their weight or more.
        static HashSet<int> Carried(SkinnedMeshRenderer skinned, ICollection<Transform> bones)
        {
            var carried = new HashSet<int>();
            var mesh = skinned.sharedMesh;
            var skeleton = skinned.bones;
            if (mesh == null)
                return carried;
            var counts = mesh.GetBonesPerVertex();
            var weights = mesh.GetAllBoneWeights();
            var at = 0;
            for (var vertex = 0; vertex < counts.Length; vertex++)
            {
                var share = 0f;
                for (var i = 0; i < counts[vertex]; i++, at++)
                {
                    var index = weights[at].boneIndex;
                    if (index < skeleton.Length && bones.Contains(skeleton[index]))
                        share += weights[at].weight;
                }

                if (share >= 0.5f)
                    carried.Add(vertex);
            }

            return carried;
        }

        public void Dispose()
        {
            if (_graph.IsValid())
                _graph.Destroy();
            Object.DestroyImmediate(_copy);
        }
    }
}
