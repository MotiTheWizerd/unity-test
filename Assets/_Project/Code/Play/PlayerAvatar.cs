using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.Play
{
    /// <summary>
    /// What the player looks like: the profile's body prefab, standing on the body's feet and turned to its heading.
    /// Hidden from the camera in first person but still casting its shadow, as in Skyrim. A placeholder mannequin today
    /// (Forge → Generate Placeholder Content); a real character with animations replaces the prefab, not this.
    /// </summary>
    public sealed class PlayerAvatar : IDisposable
    {
        readonly GameObject _model;
        readonly Renderer[] _renderers;
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
        }

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
