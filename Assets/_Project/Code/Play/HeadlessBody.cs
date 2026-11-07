using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.Play
{
    /// <summary>
    /// A character's body as seen from inside its own head (first person): each skinned mesh that its head moves is drawn
    /// a second time with the head folded away into the top of the neck, while the whole mesh casts only its shadow. So
    /// the camera at the eyes sees the arms, the chest and the feet, never the inside of the head, and the shadow keeps
    /// its head. The second meshes share the body's bones, so they move as it animates. A part of the look hanging
    /// wholly from the head casts only its shadow. Made once, under the look, and gone with it.
    /// </summary>
    sealed class HeadlessBody
    {
        readonly List<Renderer> _whole = new List<Renderer>();
        readonly List<Renderer> _headless = new List<Renderer>();

        /// <param name="look">The character's look, whose own meshes (not what it holds) are folded.</param>
        /// <param name="head">The head bone and the bones under it (<see cref="HumanoidHead.Bones"/>).</param>
        public HeadlessBody(GameObject look, Transform headBone, ICollection<Transform> head)
        {
            // One point the head's bones all fold into: at the head bone, of no size, so what they carry shrinks into it
            // and the neck closes over.
            var fold = new GameObject("Head (folded)").transform;
            fold.SetParent(headBone, false);
            fold.localScale = Vector3.zero;
            fold.gameObject.layer = look.layer;

            foreach (var renderer in look.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skinned && Moves(skinned, head))
                {
                    _whole.Add(skinned);
                    _headless.Add(Folded(skinned, head, fold));
                }
                else if (renderer.transform.HangsFrom(head))
                {
                    _whole.Add(renderer);
                }
            }
        }

        /// <summary>Seen from within (first person) or from outside, as any other character is seen.</summary>
        public void Show(bool fromWithin)
        {
            foreach (var renderer in _whole)
                renderer.shadowCastingMode = fromWithin ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var renderer in _headless)
                renderer.enabled = fromWithin;
        }

        // The mesh again, its head's bones swapped for the fold; it casts no shadow (the whole one does).
        static SkinnedMeshRenderer Folded(SkinnedMeshRenderer whole, ICollection<Transform> head, Transform fold)
        {
            var copy = new GameObject(whole.name + " (headless)");
            copy.layer = whole.gameObject.layer;
            copy.transform.SetParent(whole.transform.parent, false);
            copy.transform.localPosition = whole.transform.localPosition;
            copy.transform.localRotation = whole.transform.localRotation;
            copy.transform.localScale = whole.transform.localScale;

            var bones = whole.bones;
            for (var i = 0; i < bones.Length; i++)
            {
                if (head.Contains(bones[i]))
                    bones[i] = fold;
            }

            var headless = copy.AddComponent<SkinnedMeshRenderer>();
            headless.sharedMesh = whole.sharedMesh;
            headless.sharedMaterials = whole.sharedMaterials;
            headless.rootBone = whole.rootBone;
            headless.bones = bones;
            headless.localBounds = whole.localBounds;
            headless.quality = whole.quality;
            headless.updateWhenOffscreen = whole.updateWhenOffscreen;
            headless.skinnedMotionVectors = whole.skinnedMotionVectors;
            headless.receiveShadows = whole.receiveShadows;
            headless.lightProbeUsage = whole.lightProbeUsage;
            headless.reflectionProbeUsage = whole.reflectionProbeUsage;
            headless.shadowCastingMode = ShadowCastingMode.Off;
            for (var i = 0; i < whole.sharedMesh.blendShapeCount; i++)
                headless.SetBlendShapeWeight(i, whole.GetBlendShapeWeight(i));
            headless.enabled = false;
            return headless;
        }

        static bool Moves(SkinnedMeshRenderer skinned, ICollection<Transform> head)
        {
            if (skinned.sharedMesh == null)
                return false;
            foreach (var bone in skinned.bones)
            {
                if (head.Contains(bone))
                    return true;
            }

            return false;
        }
    }
}
