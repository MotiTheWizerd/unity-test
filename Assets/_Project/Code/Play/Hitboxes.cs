using System;
using System.Collections.Generic;
using Forge.Game.Combat;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Forge.Play
{
    /// <summary>
    /// What a blow or a shot meets of a character (<see cref="Hitbox"/>): a capsule over each part of its body (the head,
    /// neck, chest, belly and pelvis; each upper arm, forearm and hand; each thigh, shin and foot) hung from the part's
    /// humanoid bone, so it moves, leans and falls with the look as it animates. Each is fitted to the character's own
    /// skin as it stands at rest: the vertices the part's bones carry most, along the way they lie longest, as wide as
    /// most of them stand off that line; so a slight elf and a broad viking are each met where they are drawn, armor and
    /// all. Fitted once a character (by its prefab) and kept. Triggers on the <see cref="HitboxLayer"/>, with kinematic
    /// bodies, as moving colliders should have: they stop nothing, and only a look that asks for them finds them
    /// (<see cref="SceneLayers.Strikes"/>). Whose they are, the body they hang under says (<see cref="ActorLink"/>).
    /// Runtime-only; they go with the look.
    /// </summary>
    public static class Hitboxes
    {
        // Each part of the body that has a hitbox, by the humanoid bone it hangs from. The bones between (a clavicle, a
        // twist bone, the fingers, the toes, the jaw) count as the part they hang from.
        static readonly (HumanBodyBones Bone, BodyPart Part)[] Parts =
        {
            (HumanBodyBones.Hips, BodyPart.Pelvis),
            (HumanBodyBones.Spine, BodyPart.Belly),
            (HumanBodyBones.Chest, BodyPart.Chest),
            (HumanBodyBones.UpperChest, BodyPart.Chest),
            (HumanBodyBones.Neck, BodyPart.Neck),
            (HumanBodyBones.Head, BodyPart.Head),
            (HumanBodyBones.LeftUpperArm, BodyPart.UpperArm),
            (HumanBodyBones.RightUpperArm, BodyPart.UpperArm),
            (HumanBodyBones.LeftLowerArm, BodyPart.Forearm),
            (HumanBodyBones.RightLowerArm, BodyPart.Forearm),
            (HumanBodyBones.LeftHand, BodyPart.Hand),
            (HumanBodyBones.RightHand, BodyPart.Hand),
            (HumanBodyBones.LeftUpperLeg, BodyPart.Thigh),
            (HumanBodyBones.RightUpperLeg, BodyPart.Thigh),
            (HumanBodyBones.LeftLowerLeg, BodyPart.Shin),
            (HumanBodyBones.RightLowerLeg, BodyPart.Shin),
            (HumanBodyBones.LeftFoot, BodyPart.Foot),
            (HumanBodyBones.RightFoot, BodyPart.Foot),
        };

        // Of a part's skin, the share left beyond either end of its capsule, and the share no further off its line than
        // its radius: stray vertices (a braid, a strap's end) don't swell it, and it is as wide as most of it.
        const float EndShare = 0.02f;
        const float RadiusShare = 0.75f;

        // The fewest vertices a part is fitted from; with fewer, it has no hitbox.
        const int FewestPoints = 12;

        // Fitted once a character, by its prefab, in its bones' own frames.
        static readonly Dictionary<GameObject, Shape[]> Fitted = new Dictionary<GameObject, Shape[]>();

        /// <summary>
        /// Hangs the hitboxes of <paramref name="model"/>, the look of <paramref name="prefab"/>, from its bones, fitted to
        /// it as it stands at rest (fitted once a prefab, so build them on a look just made, before it animates). None
        /// without a humanoid or a skin.
        /// </summary>
        public static IReadOnlyList<Hitbox> Build(CharacterModel model, GameObject prefab)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (!Fitted.TryGetValue(prefab, out var shapes))
                Fitted[prefab] = shapes = Fit(model);

            var built = new List<Hitbox>(shapes.Length);
            foreach (var shape in shapes)
            {
                var bone = model.Bone(Parts[shape.Part].Bone);
                if (bone != null)
                    built.Add(Hang(bone, shape));
            }

            return built;
        }

        // Each part's capsule in its bone's frame, from the skin's vertices each part carries most.
        static Shape[] Fit(CharacterModel model)
        {
            var bones = new Transform[Parts.Length];
            var partOf = new Dictionary<Transform, int>();
            for (var i = 0; i < Parts.Length; i++)
            {
                bones[i] = model.Bone(Parts[i].Bone);
                if (bones[i] != null)
                    partOf[bones[i]] = i;
            }

            var points = new List<Vector3>[Parts.Length];
            var baked = new Mesh();
            try
            {
                foreach (var skinned in model.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = skinned.sharedMesh;
                    var skeleton = skinned.bones;
                    if (mesh == null || skeleton.Length == 0)
                        continue;
                    var owners = new int[skeleton.Length];
                    for (var b = 0; b < skeleton.Length; b++)
                        owners[b] = PartOf(skeleton[b], partOf);

                    // Unscaled, so the renderer's transform places it as drawn.
                    skinned.BakeMesh(baked, false);
                    var vertices = baked.vertices;
                    var counts = mesh.GetBonesPerVertex();
                    var weights = mesh.GetAllBoneWeights();
                    var at = 0;
                    for (var vertex = 0; vertex < counts.Length; vertex++)
                    {
                        var part = Heaviest(weights, at, counts[vertex], owners);
                        at += counts[vertex];
                        if (part < 0 || vertex >= vertices.Length)
                            continue;
                        (points[part] ??= new List<Vector3>()).Add(
                            bones[part].InverseTransformPoint(skinned.transform.TransformPoint(vertices[vertex])));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }

            var shapes = new List<Shape>();
            for (var i = 0; i < Parts.Length; i++)
            {
                if (points[i] != null && TryFit(points[i], out var shape))
                {
                    shape.Part = i;
                    shapes.Add(shape);
                }
            }

            return shapes.ToArray();
        }

        // The part a bone counts as: its own, else the nearest it hangs from; -1 for none (above the hips).
        static int PartOf(Transform bone, Dictionary<Transform, int> partOf)
        {
            for (var at = bone; at != null; at = at.parent)
            {
                if (partOf.TryGetValue(at, out var part))
                    return part;
            }

            return -1;
        }

        // The part that carries most of one vertex's skinning weight (its `count` bone weights from `start`); -1 for none.
        static int Heaviest(NativeArray<BoneWeight1> weights, int start, int count, int[] owners)
        {
            var heaviest = -1;
            var most = 0f;
            for (var i = 0; i < count; i++)
            {
                var part = Owner(weights[start + i], owners);
                if (part < 0)
                    continue;
                var carried = 0f;
                for (var j = 0; j < count; j++)
                {
                    if (Owner(weights[start + j], owners) == part)
                        carried += weights[start + j].weight;
                }

                if (carried > most)
                {
                    most = carried;
                    heaviest = part;
                }
            }

            return heaviest;
        }

        static int Owner(BoneWeight1 weight, int[] owners) => weight.boneIndex < owners.Length ? owners[weight.boneIndex] : -1;

        // A capsule over `points` (one part's skin, in its bone's frame): along the way they lie longest, between the
        // ends most of them lie within, as wide as most of them stand off that line.
        static bool TryFit(List<Vector3> points, out Shape shape)
        {
            shape = default;
            var count = points.Count;
            if (count < FewestPoints)
                return false;

            var mean = Vector3.zero;
            foreach (var point in points)
                mean += point;
            mean /= count;
            float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
            foreach (var point in points)
            {
                var d = point - mean;
                xx += d.x * d.x;
                xy += d.x * d.y;
                xz += d.x * d.z;
                yy += d.y * d.y;
                yz += d.y * d.z;
                zz += d.z * d.z;
            }

            var axis = LongestWay(xx, xy, xz, yy, yz, zz);
            var along = new float[count];
            var off = new float[count];
            for (var i = 0; i < count; i++)
            {
                var d = points[i] - mean;
                along[i] = Vector3.Dot(d, axis);
                off[i] = (d - axis * along[i]).magnitude;
            }

            Array.Sort(along);
            Array.Sort(off);
            var low = along[(int)(EndShare * (count - 1))];
            var high = along[(int)((1f - EndShare) * (count - 1))];
            var radius = off[(int)(RadiusShare * (count - 1))];
            if (!(radius > 0f))
                return false;

            shape = new Shape
            {
                Center = mean + axis * ((low + high) * 0.5f),
                Turn = Quaternion.FromToRotation(Vector3.up, axis),
                Radius = radius,
                Height = Mathf.Max(high - low, radius * 2f),
            };
            return true;
        }

        // The way a cloud of points lies longest: its covariance's leading axis, by power iteration from the widest of the
        // three axes.
        static Vector3 LongestWay(float xx, float xy, float xz, float yy, float yz, float zz)
        {
            var way = xx >= yy && xx >= zz ? Vector3.right : yy >= zz ? Vector3.up : Vector3.forward;
            for (var i = 0; i < 32; i++)
            {
                var next = new Vector3(
                    xx * way.x + xy * way.y + xz * way.z,
                    xy * way.x + yy * way.y + yz * way.z,
                    xz * way.x + yz * way.y + zz * way.z);
                if (next.sqrMagnitude < 1e-20f)
                    break;
                way = next.normalized;
            }

            return way;
        }

        // A part's hitbox hung from its bone: the capsule a trigger along its own up, moved with the bone every frame (a
        // kinematic body, as a moving collider should have).
        static Hitbox Hang(Transform bone, Shape shape)
        {
            var (humanBone, part) = Parts[shape.Part];
            var box = new GameObject($"Hitbox {humanBone}") { layer = HitboxLayer.Index };
            box.transform.SetParent(bone, false);
            box.transform.localPosition = shape.Center;
            box.transform.localRotation = shape.Turn;
            var hitbox = box.AddComponent<Hitbox>();
            hitbox.Part = part;
            var capsule = box.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.direction = 1;
            capsule.radius = shape.Radius;
            capsule.height = shape.Height;
            var body = box.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return hitbox;
        }

        // One part's capsule, in its bone's frame: its middle, turned so its up lies along it, its radius and its whole
        // length, ends and all. Part: its index in Parts.
        struct Shape
        {
            public int Part;
            public Vector3 Center;
            public Quaternion Turn;
            public float Radius;
            public float Height;
        }
    }
}
