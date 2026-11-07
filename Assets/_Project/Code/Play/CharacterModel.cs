using System;
using System.Collections.Generic;
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
        readonly Dictionary<GameObject, Renderer[]> _attached = new Dictionary<GameObject, Renderer[]>();
        bool _shadowsOnly;

        /// <param name="body">The body object the model rides on.</param>
        public CharacterModel(Transform body, GameObject prefab)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

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

        /// <summary>How long its draw plays, seconds; 0 without one.</summary>
        public float DrawSeconds => _animation?.DrawSeconds ?? 0f;

        /// <summary>How long its sheathe plays, seconds; 0 without one.</summary>
        public float SheatheSeconds => _animation?.SheatheSeconds ?? 0f;

        /// <summary>
        /// Hangs <paramref name="look"/> from the humanoid bone named <paramref name="bone"/> ("RightHand"), held by
        /// <paramref name="grip"/> (a part of it, or itself): the grip's pivot and axes on the bone's attach point. False,
        /// and the look destroyed, when the model has no such bone.
        /// </summary>
        public bool Attach(GameObject look, Transform grip, string bone)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));
            if (!TryPoint(bone, out var point))
            {
                UnityEngine.Object.Destroy(look);
                return false;
            }

            // Its own size whatever the skeleton's scale, then turned and moved so that its grip lies on the point.
            var size = look.transform.localScale;
            look.transform.SetParent(point, false);
            var boneScale = point.lossyScale.x;
            if (boneScale > 0f)
                look.transform.localScale = size / boneScale;
            var gripTurn = Quaternion.Inverse(look.transform.rotation) * grip.rotation;
            look.transform.rotation = point.rotation * Quaternion.Inverse(gripTurn);
            look.transform.position += point.position - grip.position;

            RemoveColliders(look);
            SetLayer(look.transform, Root.layer);
            var renderers = look.GetComponentsInChildren<Renderer>(true);
            _attached.Add(look, renderers);
            foreach (var renderer in renderers)
            {
                _renderers.Add(renderer);
                Draw(renderer);
            }

            return true;
        }

        /// <summary>Takes <paramref name="look"/> off and destroys it.</summary>
        public void Detach(GameObject look)
        {
            if (look == null || !_attached.TryGetValue(look, out var renderers))
                return;
            _attached.Remove(look);
            foreach (var renderer in renderers)
                _renderers.Remove(renderer);
            UnityEngine.Object.Destroy(look);
        }

        public void Dispose()
        {
            if (Root != null)
                UnityEngine.Object.Destroy(Root);
        }

        // A bone's attach point, made the first time it is asked for: a hand's grip, any other bone itself.
        bool TryPoint(string bone, out Transform point)
        {
            point = null;
            if (_humanoid == null || !Enum.TryParse(bone, out HumanBodyBones id) || id == HumanBodyBones.LastBone)
                return false;
            if (_points.TryGetValue(id, out point))
                return true;
            var boneTransform = _humanoid.GetBoneTransform(id);
            if (boneTransform == null)
                return false;

            point = new GameObject("Holds " + bone).transform;
            point.SetParent(boneTransform, false);
            point.gameObject.layer = Root.layer;
            if (TryGrip(id, boneTransform, out var grip))
                point.SetPositionAndRotation(grip.position, grip.rotation);
            _points.Add(id, point);
            return true;
        }

        // A hand's grip from its bones: a knuckle (the middle finger's, else the index's) and the thumb.
        bool TryGrip(HumanBodyBones id, Transform hand, out Pose grip)
        {
            grip = default;
            bool right;
            if (id == HumanBodyBones.RightHand)
                right = true;
            else if (id == HumanBodyBones.LeftHand)
                right = false;
            else
                return false;

            var knuckle = _humanoid.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            if (knuckle == null)
                knuckle = _humanoid.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var thumb = _humanoid.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            if (knuckle == null || thumb == null)
                return false;
            grip = GripFrame.Hand(hand.position, knuckle.position, thumb.position, right);
            return true;
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
