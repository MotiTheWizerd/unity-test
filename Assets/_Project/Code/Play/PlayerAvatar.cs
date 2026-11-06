using System;
using Forge.Game.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.Play
{
    /// <summary>
    /// What the player looks like: the profile's body prefab, standing on the body's feet and turned to its heading.
    /// Hidden from the camera in first person but still casting its shadow, as in Skyrim. A humanoid character
    /// (Content/Characters) animates as the body moves (<see cref="HumanoidAnimation"/>); a model without animations
    /// (the placeholder mannequin) just rides along.
    /// </summary>
    public sealed class PlayerAvatar : IDisposable
    {
        readonly GameObject _model;
        readonly Renderer[] _renderers;
        readonly HumanoidAnimation _animation;
        bool? _shown;

        /// <param name="body">The player's body object, which the model rides on.</param>
        public PlayerAvatar(Transform body, GameObject prefab)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            _model = UnityEngine.Object.Instantiate(prefab, body, false);
            _model.name = prefab.name;
            // The body is the collider; the model's own would trip it and the camera.
            foreach (var collider in _model.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.Destroy(collider);
            SetLayer(_model.transform, body.gameObject.layer);
            _renderers = _model.GetComponentsInChildren<Renderer>(true);
            // Characters skip posing while off screen; the player's never is, and in first person its shadow still walks.
            foreach (var animator in _model.GetComponentsInChildren<Animator>(true))
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animation = HumanoidAnimation.On(_model);
        }

        /// <summary>Shows how the body moved (walking, running, standing); a model without animations just rides along.</summary>
        public void Animate(BodyMotion motion, float deltaTime) => _animation?.Show(motion, deltaTime);

        /// <summary>Turns the model to <paramref name="heading"/> (compass degrees).</summary>
        public void Face(float heading) => _model.transform.localRotation = Quaternion.Euler(0f, heading, 0f);

        /// <summary>Shows the model, or leaves only its shadow (first person).</summary>
        public void Show(bool shown)
        {
            if (_shown == shown)
                return;
            _shown = shown;
            var mode = shown ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            foreach (var renderer in _renderers)
                renderer.shadowCastingMode = mode;
        }

        public void Dispose()
        {
            if (_model != null)
                UnityEngine.Object.Destroy(_model);
        }

        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform)
                SetLayer(child, layer);
        }
    }
}
