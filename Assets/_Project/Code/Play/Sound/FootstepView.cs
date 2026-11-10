using System;
using Forge.Game.Motion;
using Forge.Game.Player;
using UnityEngine;

namespace Forge.Play.Sound
{
    /// <summary>
    /// A character's footsteps: each time one of its feet comes down in its animation (<see cref="StepDetector"/>),
    /// the step of its gait (<see cref="Gaits"/>) on what is underfoot there (<see cref="GroundSurfaces"/>) plays where
    /// that foot is (<see cref="FootstepKeys"/>). Nothing while it stands or is in the air. It reads the pose its
    /// animation last took, a frame behind, as the body it rides is; both feet alike, so any clip steps in time.
    /// </summary>
    public sealed class FootstepView
    {
        static readonly HumanBodyBones[] FeetBones = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };

        readonly SoundPlayer _sounds;
        readonly GroundSurfaces _surfaces;
        readonly LocomotionSettings _paces;
        readonly Transform[] _feet;
        readonly StepDetector[] _steps;
        readonly Vector3[] _were;

        /// <param name="model">The character, whose feet step.</param>
        /// <param name="surfaces">What is underfoot; null: nothing in particular anywhere.</param>
        /// <param name="paces">Its paces, which its gaits are told apart by.</param>
        public FootstepView(CharacterModel model, SoundPlayer sounds, GroundSurfaces surfaces, LocomotionSettings paces)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
            _surfaces = surfaces;
            _paces = paces ?? throw new ArgumentNullException(nameof(paces));
            _feet = Array.ConvertAll(FeetBones, model.Bone);
            _steps = Array.ConvertAll(FeetBones, _ => new StepDetector());
            _were = new Vector3[FeetBones.Length];
            Settle();
        }

        /// <summary>Hears the feet over the last <paramref name="deltaTime"/> seconds, the body moving as <paramref name="motion"/> says.</summary>
        public void Tick(BodyMotion motion, float deltaTime)
        {
            var gait = Gaits.Of(motion, _paces);
            for (var i = 0; i < _feet.Length; i++)
            {
                var foot = _feet[i];
                if (foot == null)
                    continue;
                var at = foot.position;
                if (gait == Gait.Still || !(deltaTime > 0f))
                    _steps[i].Reset();
                else if (_steps[i].Update((at - _were[i]).magnitude / deltaTime, motion.GroundSpeed))
                    _sounds.PlayFirst(FootstepKeys.For(_surfaces?.At(at), gait), at);
                _were[i] = at;
            }
        }

        /// <summary>The scene moved by <paramref name="delta"/> under the world (a rebase): where the feet were moves with it, so it is no stride.</summary>
        public void Shift(Vector3 delta)
        {
            for (var i = 0; i < _were.Length; i++)
                _were[i] += delta;
        }

        // Where the feet are now, as where they were: no step until they move on from here.
        void Settle()
        {
            for (var i = 0; i < _feet.Length; i++)
            {
                if (_feet[i] != null)
                    _were[i] = _feet[i].position;
            }
        }
    }
}
