using Forge.Game.Cameras;
using Forge.World;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// What only the player needs of its look (<see cref="CharacterModel"/>): its eyes, which the first-person camera
    /// rides (<see cref="IEyePoint"/>) as the head sways, leans into a blow or stoops into a stance, at the character's
    /// own eye height (<see cref="CharacterModel.Eyes"/>); and its animators always animating, since the player is never
    /// off screen, seen from inside its own head. Made anew with each look; the eyes go with the look.
    /// </summary>
    public sealed class PlayerEyes : IEyePoint
    {
        readonly CharacterModel _model;
        readonly Transform _eyes;

        /// <param name="eyeHeight">Meters from the feet to the eyes as it stands at rest.</param>
        public PlayerEyes(CharacterModel model, float eyeHeight)
        {
            _model = model ?? throw new System.ArgumentNullException(nameof(model));
            foreach (var animator in model.Animators)
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _eyes = model.Eyes(eyeHeight);
        }

        /// <summary>Where the eyes are as the head last posed, from the feet in the body's own frame (its model's turn).</summary>
        public bool TryGetEyes(out System.Numerics.Vector3 eyes)
        {
            eyes = default;
            if (_eyes == null)
                return false;
            var model = _model.Root.transform;
            eyes = (Quaternion.Inverse(model.rotation) * (_eyes.position - model.parent.position)).ToNumerics();
            return true;
        }
    }
}
