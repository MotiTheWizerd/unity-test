using Forge.Game.Motion;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Plays a humanoid's animations from how its body moves (<see cref="BodyMotion"/>): the one thing that talks to its
    /// Animator. The animator controller (Forge → Characters → Build Characters) blends the ground moves by
    /// <see cref="Speed"/>, plays <see cref="Pace"/> faster past its fastest clip, and turns to the air moves by
    /// <see cref="Grounded"/> and <see cref="VerticalSpeed"/>; <see cref="TopSpeed"/> is the fastest clip's speed, which
    /// the builder leaves as that parameter's default. The body moves itself; the animations only show it (no root motion).
    /// </summary>
    public sealed class HumanoidAnimation
    {
        public const string Speed = "Speed";
        public const string Pace = "Pace";
        public const string TopSpeed = "TopSpeed";
        public const string Grounded = "Grounded";
        public const string VerticalSpeed = "VerticalSpeed";

        // Seconds the blend takes to catch up with a new speed, so a stop or a start eases rather than snaps.
        const float SpeedDamping = 0.1f;

        static readonly int SpeedId = Animator.StringToHash(Speed);
        static readonly int PaceId = Animator.StringToHash(Pace);
        static readonly int TopSpeedId = Animator.StringToHash(TopSpeed);
        static readonly int GroundedId = Animator.StringToHash(Grounded);
        static readonly int VerticalSpeedId = Animator.StringToHash(VerticalSpeed);

        readonly Animator _animator;
        readonly float _topSpeed;

        HumanoidAnimation(Animator animator)
        {
            _animator = animator;
            _animator.applyRootMotion = false;
            _topSpeed = animator.GetFloat(TopSpeedId);
        }

        /// <summary>The animations of <paramref name="model"/>, or null when it has none to play (no humanoid Animator with a controller).</summary>
        public static HumanoidAnimation On(GameObject model)
        {
            var animator = model.GetComponentInChildren<Animator>();
            return animator != null && animator.runtimeAnimatorController != null && animator.isHuman
                ? new HumanoidAnimation(animator)
                : null;
        }

        /// <summary>Shows <paramref name="motion"/>, the body's last <paramref name="deltaTime"/> seconds.</summary>
        public void Show(BodyMotion motion, float deltaTime)
        {
            var stride = StrideMatch.For(motion.GroundSpeed, _animator.humanScale, _topSpeed);
            _animator.SetFloat(SpeedId, stride.BlendSpeed, SpeedDamping, deltaTime);
            _animator.SetFloat(PaceId, stride.Pace);
            _animator.SetBool(GroundedId, motion.Grounded);
            _animator.SetFloat(VerticalSpeedId, motion.VerticalSpeed);
        }
    }
}
