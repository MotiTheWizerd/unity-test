using System;
using Forge.Game.Cameras;
using Forge.Game.Motion;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What the player looks like: the profile's character (<see cref="CharacterModel"/>) plus what only the player
    /// needs. In first person the camera is inside its head, as in a true first person: the body, what it holds and what
    /// it does all show from the eyes, the head only as its shadow (<see cref="CharacterModel.FirstPerson"/>), and the
    /// camera rides the eyes as the head moves (<see cref="IEyePoint"/>), at the character's own eye height
    /// (<see cref="EyeHeight"/>). Always animated, since the player is never off screen.
    /// </summary>
    public sealed class PlayerAvatar : IDisposable, IEyePoint
    {
        readonly Transform _eyes;

        /// <param name="body">The player's body object, which the model rides on.</param>
        /// <param name="hover">How far the body's feet stay above the ground (its controller's skin).</param>
        /// <param name="eyeHeight">Meters from the feet to the eyes as it stands at rest, for a look that can't tell (no head).</param>
        public PlayerAvatar(Transform body, GameObject prefab, float hover, float eyeHeight)
        {
            Model = new CharacterModel(body, prefab, hover);
            foreach (var animator in Model.Animators)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EyeHeight = Model.TryGetEyeHeight(out var own) ? own : eyeHeight;
            _eyes = Model.Eyes(EyeHeight);
        }

        /// <summary>The look, which what the player holds hangs from.</summary>
        public CharacterModel Model { get; }

        /// <summary>Meters from the feet to the eyes as it stands at rest: its own head's (a woman's lower than a man's).</summary>
        public float EyeHeight { get; }

        /// <summary>Seen from inside its head (first person), or from outside (third).</summary>
        public bool FirstPerson
        {
            get => Model.FirstPerson;
            set => Model.FirstPerson = value;
        }

        /// <summary>Shows how the body moved (walking, running, standing).</summary>
        public void Animate(BodyMotion motion, float deltaTime) => Model.Animate(motion, deltaTime);

        /// <summary>Turns the model to <paramref name="heading"/> (compass degrees).</summary>
        public void Face(float heading) => Model.Face(heading);

        /// <summary>Where the eyes are as the head last posed, from the feet in the body's own frame (its model's turn).</summary>
        public bool TryGetEyes(out System.Numerics.Vector3 eyes)
        {
            eyes = default;
            if (_eyes == null)
                return false;
            var model = Model.Root.transform;
            eyes = (Quaternion.Inverse(model.rotation) * (_eyes.position - model.parent.position)).ToNumerics();
            return true;
        }

        public void Dispose() => Model.Dispose();
    }
}
