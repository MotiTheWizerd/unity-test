using System;
using Forge.Game.Motion;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.Play
{
    /// <summary>
    /// What the player looks like: the profile's character (<see cref="CharacterModel"/>) plus what only the player
    /// needs. Hidden from the camera in first person but still casting its shadow, as in Skyrim, and always animated,
    /// since the player is never off screen and its first-person shadow still walks.
    /// </summary>
    public sealed class PlayerAvatar : IDisposable
    {
        readonly CharacterModel _model;
        bool? _shown;

        /// <param name="body">The player's body object, which the model rides on.</param>
        public PlayerAvatar(Transform body, GameObject prefab)
        {
            _model = new CharacterModel(body, prefab);
            foreach (var animator in _model.Animators)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>Shows how the body moved (walking, running, standing).</summary>
        public void Animate(BodyMotion motion, float deltaTime) => _model.Animate(motion, deltaTime);

        /// <summary>Turns the model to <paramref name="heading"/> (compass degrees).</summary>
        public void Face(float heading) => _model.Face(heading);

        /// <summary>Shows the model, or leaves only its shadow (first person).</summary>
        public void Show(bool shown)
        {
            if (_shown == shown)
                return;
            _shown = shown;
            var mode = shown ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            foreach (var renderer in _model.Renderers)
                renderer.shadowCastingMode = mode;
        }

        public void Dispose() => _model.Dispose();
    }
}
