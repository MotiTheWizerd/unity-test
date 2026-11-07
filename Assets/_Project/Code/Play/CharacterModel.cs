using System;
using System.Collections.Generic;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.Game.Motion;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.Play
{
    /// <summary>
    /// A character's look riding on its body: the character prefab (Content/Characters) standing on the body's feet,
    /// turned to the body's heading and animated as the body moves (<see cref="HumanoidAnimation"/>). The body is the
    /// collider, so the model's own colliders go, and the model shares the body's layer. Whoever has a body uses it: the
    /// player (<see cref="PlayerAvatar"/>), and characters of the world later. A model without animations just rides along.
    /// <para>
    /// Things it holds or wears hang from its humanoid bones (<see cref="Attach"/>): a hand holds by its grip
    /// (<see cref="GripFrame"/>), worked out from the hand's bones once, so they move with the hand as it animates; any
    /// other bone holds at the bone. What is attached is part of the look: on its layer, without colliders, and drawn or
    /// shadow-only with the rest (<see cref="ShadowsOnly"/>).
    /// </para>
    /// </summary>
    public sealed class CharacterModel : IDisposable
    {
        readonly HumanoidAnimation _animation;
        readonly Animator _humanoid;
        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly Dictionary<HumanBodyBones, Transform> _points = new Dictionary<HumanBodyBones, Transform>();
        readonly Dictionary<HumanBodyBones, Transform> _sheaths = new Dictionary<HumanBodyBones, Transform>();
        readonly Dictionary<GameObject, Held> _attached = new Dictionary<GameObject, Held>();
        readonly GameObject _prefab;
        bool _shadowsOnly;
        bool _inHand;

        /// <param name="body">The body object the model rides on.</param>
        public CharacterModel(Transform body, GameObject prefab)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            _prefab = prefab;
            Root = UnityEngine.Object.Instantiate(prefab, body, false);
            Root.name = prefab.name;
            RemoveColliders(Root);
            SetLayer(Root.transform, body.gameObject.layer);
            _renderers.AddRange(Root.GetComponentsInChildren<Renderer>(true));
            Animators = Root.GetComponentsInChildren<Animator>(true);
            foreach (var animator in Animators)
            {
                if (animator.isHuman)
                {
                    _humanoid = animator;
                    break;
                }
            }

            _animation = HumanoidAnimation.On(Root);
        }

        /// <summary>
        /// <paramref name="prefab"/>'s look standing still (its idle) under <paramref name="parent"/>, for a picture of the
        /// character (the inventory's); destroying the object returned is all it takes to take it away, and what is
        /// attached to it goes with it.
        /// </summary>
        public static CharacterModel StandingStill(Transform parent, GameObject prefab)
        {
            var model = new CharacterModel(parent, prefab);
            model.Animate(BodyMotion.Still, 0f);
            return model;
        }

        /// <summary>The model's object, a child of the body.</summary>
        public GameObject Root { get; }

        /// <summary>What it draws, what is attached to it included.</summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;

        public Animator[] Animators { get; }

        /// <summary>Only its shadow is drawn (first person), what is attached included, now and later.</summary>
        public bool ShadowsOnly
        {
            get => _shadowsOnly;
            set
            {
                _shadowsOnly = value;
                foreach (var renderer in _renderers)
                    Draw(renderer);
            }
        }

        /// <summary>Turns the model to <paramref name="heading"/> (compass degrees).</summary>
        public void Face(float heading) => Root.transform.localRotation = Quaternion.Euler(0f, heading, 0f);

        /// <summary>Shows how the body moved over the last <paramref name="deltaTime"/> seconds (standing, walking, a jump).</summary>
        public void Animate(BodyMotion motion, float deltaTime) => _animation?.Show(motion, deltaTime);

        /// <summary>Shows its weapons becoming <paramref name="readiness"/> (a draw or a sheathe playing, or done).</summary>
        public void Animate(Readiness readiness) => _animation?.Show(readiness);

        /// <summary>Plays the action <paramref name="move"/> (a slash) on its arms; false when it has no such move.</summary>
        public bool Play(string move) => _animation != null && _animation.Play(move);

        /// <summary>How its move <paramref name="move"/> plays, measured from the clip (<see cref="SwingFinder"/>); false when it has none.</summary>
        public bool TryTime(string move, out MoveTiming timing)
        {
            timing = default;
            return _animation != null && SwingFinder.TryFind(_prefab, _animation.Clip(move), out timing);
        }

        /// <summary>How long its draw plays, seconds; 0 without one.</summary>
        public float DrawSeconds => _animation?.DrawSeconds ?? 0f;

        /// <summary>How long its sheathe plays, seconds; 0 without one.</summary>
        public float SheatheSeconds => _animation?.SheatheSeconds ?? 0f;

        /// <summary>How far into its draw the hand takes the hilt (when the weapon leaves the sheath), 0 to 1; 1 when unknown.</summary>
        public float DrawnAt => SheathFinder.TryFind(_prefab, _animation?.DrawClip, out var sheaths) ? sheaths.Moment : 1f;

        /// <summary>How far into its sheathe the hand lets go at the sheath, 0 to 1; 1 when unknown.</summary>
        public float SheathedAt => SheathFinder.TryFind(_prefab, _animation?.SheatheClip, out var sheaths) ? sheaths.Moment : 1f;

        /// <summary>
        /// Whether what sheathes is in hand (weapons drawn) or in its sheath at the hip, where the model's draw clip takes it
        /// from (<see cref="SheathFinder"/>); a model that can't tell keeps it in hand.
        /// </summary>
        public bool InHand
        {
            get => _inHand;
            set
            {
                if (_inHand == value)
                    return;
                _inHand = value;
                foreach (var held in _attached.Values)
                {
                    if (held.Sheathes)
                        Place(held);
                }
            }
        }

        /// <summary>
        /// Hangs <paramref name="look"/> from the humanoid bone named <paramref name="bone"/> ("RightHand"), held by
        /// <paramref name="grip"/> (a part of it, or itself): the grip's pivot and axes on the bone's attach point; or, when
        /// it <paramref name="sheathes"/> and is not <see cref="InHand"/>, on that hand's sheath. False, and the look
        /// destroyed, when the model has no such bone.
        /// </summary>
        public bool Attach(GameObject look, Transform grip, string bone, bool sheathes = false)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));
            if (!Enum.TryParse(bone, out HumanBodyBones id) || !TryPoint(id, out _))
            {
                UnityEngine.Object.Destroy(look);
                return false;
            }

            var held = new Held
            {
                Look = look,
                Grip = grip,
                Bone = id,
                Sheathes = sheathes,
                Size = look.transform.localScale,
                Renderers = look.GetComponentsInChildren<Renderer>(true),
            };
            held.Reach = ReachOf(held);
            RemoveColliders(look);
            SetLayer(look.transform, Root.layer);
            _attached.Add(look, held);
            foreach (var renderer in held.Renderers)
            {
                _renderers.Add(renderer);
                Draw(renderer);
            }

            Place(held);
            return true;
        }

        /// <summary>Takes <paramref name="look"/> off and destroys it.</summary>
        public void Detach(GameObject look)
        {
            if (look == null || !_attached.TryGetValue(look, out var held))
                return;
            _attached.Remove(look);
            foreach (var renderer in held.Renderers)
                _renderers.Remove(renderer);
            UnityEngine.Object.Destroy(look);
        }

        /// <summary>
        /// Where the blade of what <paramref name="bone"/> ("RightHand") holds is now, in the hand (scene space): from the
        /// grip out along the held thing's +Y to its farthest point; false when the bone holds nothing, or it is sheathed.
        /// </summary>
        public bool TryGetBlade(string bone, out Vector3 from, out Vector3 to)
        {
            from = to = default;
            if (!Enum.TryParse(bone, out HumanBodyBones id))
                return false;
            foreach (var held in _attached.Values)
            {
                if (held.Bone != id || (held.Sheathes && !_inHand) || held.Reach <= 0f)
                    continue;
                from = held.Grip.position;
                to = from + held.Grip.up * (held.Reach * held.Grip.lossyScale.y);
                return true;
            }

            return false;
        }

        // How far a held thing reaches out of the fist along its grip's +Y, in the grip's own units: the farthest corner
        // of its meshes' bounds.
        static float ReachOf(Held held)
        {
            var reach = 0f;
            foreach (var renderer in held.Renderers)
            {
                var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;
                var bounds = mesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var inGrip = held.Grip.InverseTransformPoint(renderer.transform.TransformPoint(local));
                    reach = Mathf.Max(reach, inGrip.y);
                }
            }

            return reach;
        }

        // In the hand, or in its sheath when sheathed and the model knows where that is.
        void Place(Held held)
        {
            var point = held.Sheathes && !_inHand && TrySheath(held.Bone, out var sheath) ? sheath : _points[held.Bone];

            // Its own size whatever the skeleton's scale, then turned and moved so that its grip lies on the point.
            var look = held.Look.transform;
            look.SetParent(point, false);
            var boneScale = point.lossyScale.x;
            look.localScale = boneScale > 0f ? held.Size / boneScale : held.Size;
            var gripTurn = Quaternion.Inverse(look.rotation) * held.Grip.rotation;
            look.rotation = point.rotation * Quaternion.Inverse(gripTurn);
            look.position += point.position - held.Grip.position;
        }

        // A hand's sheath at the hip, made the first time it is asked for, from where the draw clip takes the hilt.
        bool TrySheath(HumanBodyBones hand, out Transform sheath)
        {
            sheath = null;
            if (hand != HumanBodyBones.RightHand && hand != HumanBodyBones.LeftHand)
                return false;
            if (_sheaths.TryGetValue(hand, out sheath))
                return true;
            var hips = _humanoid != null ? _humanoid.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips == null || !SheathFinder.TryFind(_prefab, _animation?.DrawClip, out var sheaths))
                return false;

            var pose = sheaths.Of(hand == HumanBodyBones.RightHand);
            sheath = new GameObject("Sheath " + hand).transform;
            sheath.SetParent(hips, false);
            sheath.gameObject.layer = Root.layer;
            sheath.SetPositionAndRotation(hips.position + hips.rotation * pose.position, hips.rotation * pose.rotation);
            _sheaths.Add(hand, sheath);
            return true;
        }

        public void Dispose()
        {
            if (Root != null)
                UnityEngine.Object.Destroy(Root);
        }

        // A bone's attach point, made the first time it is asked for: a hand's grip, any other bone itself.
        bool TryPoint(HumanBodyBones id, out Transform point)
        {
            point = null;
            if (_humanoid == null || id < 0 || id >= HumanBodyBones.LastBone)
                return false;
            if (_points.TryGetValue(id, out point))
                return true;
            var boneTransform = _humanoid.GetBoneTransform(id);
            if (boneTransform == null)
                return false;

            point = new GameObject("Holds " + id).transform;
            point.SetParent(boneTransform, false);
            point.gameObject.layer = Root.layer;
            if ((id == HumanBodyBones.RightHand || id == HumanBodyBones.LeftHand) &&
                HumanoidHands.TryGrip(_humanoid, id == HumanBodyBones.RightHand, out var grip))
                point.SetPositionAndRotation(grip.position, grip.rotation);
            _points.Add(id, point);
            return true;
        }

        // A thing hung from the model: how it is held, where, and whether it goes to a sheath.
        sealed class Held
        {
            public GameObject Look;
            public Transform Grip;
            public HumanBodyBones Bone;
            public bool Sheathes;
            public Vector3 Size;
            public Renderer[] Renderers;
            public float Reach;
        }

        void Draw(Renderer renderer) => renderer.shadowCastingMode = _shadowsOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;

        static void RemoveColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }
        }

        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform)
                SetLayer(child, layer);
        }
    }
}
