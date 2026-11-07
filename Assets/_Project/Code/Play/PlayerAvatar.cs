using System;
using Forge.Game.Motion;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What the player looks like: the profile's character (<see cref="CharacterModel"/>) plus what only the player
    /// needs. Hidden from the camera in first person but still casting its shadow, as in Skyrim, what it holds included,
    /// and always animated, since the player is never off screen and its first-person shadow still walks.
    /// </summary>
    public sealed class PlayerAvatar : IDisposable
    {
        /// <param name="body">The player's body object, which the model rides on.</param>
        public PlayerAvatar(Transform body, GameObject prefab)
        {
            Model = new CharacterModel(body, prefab);
            foreach (var animator in Model.Animators)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>The look, which what the player holds hangs from.</summary>
        public CharacterModel Model { get; }

        /// <summary>Shows how the body moved (walking, running, standing).</summary>
        public void Animate(BodyMotion motion, float deltaTime) => Model.Animate(motion, deltaTime);

        /// <summary>Turns the model to <paramref name="heading"/> (compass degrees).</summary>
        public void Face(float heading) => Model.Face(heading);

        /// <summary>Shows the model, or leaves only its shadow (first person).</summary>
        public void Show(bool shown)
        {
            if (Model.ShadowsOnly != shown)
                return;
            Model.ShadowsOnly = !shown;
        }

        public void Dispose() => Model.Dispose();
    }
}
