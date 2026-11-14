using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A skinned piece of a character worn on its body: a mesh exported on its own copy of the body's skeleton (a head;
    /// armour to come), drawn by the body's own bones, found by name, so it bends with the body as one skin. The piece's
    /// bones stood where the body's stand when both were made (blender-tests pieces.py exports them from one skeleton), so
    /// its bind poses hold on the body's bones as they do on its own.
    /// </summary>
    public static class SkinnedPiece
    {
        /// <summary>
        /// <paramref name="piece"/> (a renderer of a piece's prefab, left as it is) drawn on the body
        /// <paramref name="root"/> is the model of: a new object under the root, named as the piece's renderer and where
        /// it is in its own model, on the root's layer, with its mesh, materials and bounds, its bones the root's of the
        /// same names.
        /// </summary>
        /// <exception cref="ArgumentException">The body has no bone of one of the piece's bones' names.</exception>
        public static SkinnedMeshRenderer Stitch(Transform root, SkinnedMeshRenderer piece)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (piece == null)
                throw new ArgumentNullException(nameof(piece));

            var bones = BonesByName(root);
            var pieceBones = piece.bones;
            var onBody = new Transform[pieceBones.Length];
            for (var i = 0; i < pieceBones.Length; i++)
                onBody[i] = pieceBones[i] != null ? BodyBone(bones, pieceBones[i].name, piece) : null;

            var worn = new GameObject(piece.name) { layer = root.gameObject.layer };
            var pose = piece.transform.root.worldToLocalMatrix * piece.transform.localToWorldMatrix;
            worn.transform.SetParent(root, false);
            worn.transform.SetLocalPositionAndRotation(pose.GetColumn(3), pose.rotation);
            worn.transform.localScale = pose.lossyScale;

            var skinned = worn.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = piece.sharedMesh;
            skinned.sharedMaterials = piece.sharedMaterials;
            skinned.bones = onBody;
            skinned.rootBone = piece.rootBone != null ? BodyBone(bones, piece.rootBone.name, piece) : null;
            skinned.localBounds = piece.localBounds;
            skinned.shadowCastingMode = piece.shadowCastingMode;
            skinned.receiveShadows = piece.receiveShadows;
            skinned.updateWhenOffscreen = piece.updateWhenOffscreen;
            skinned.quality = piece.quality;
            return skinned;
        }

        // Every object under the root by name, the first of a name (a model's bones' names are its own).
        static Dictionary<string, Transform> BonesByName(Transform root)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
                bones.TryAdd(bone.name, bone);
            return bones;
        }

        static Transform BodyBone(Dictionary<string, Transform> bones, string name, SkinnedMeshRenderer piece) =>
            bones.TryGetValue(name, out var bone)
                ? bone
                : throw new ArgumentException($"The body has no bone '{name}' for the piece '{piece.name}'.", nameof(piece));
    }
}
