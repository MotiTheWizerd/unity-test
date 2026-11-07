using Forge.Game.Equipment;
using Forge.Game.Motion;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// Plays a humanoid's animations from how its body moves (<see cref="BodyMotion"/>) and where its weapons are
    /// (<see cref="Readiness"/>): the one thing that talks to its Animator. The animator controller (Forge → Characters →
    /// Build Characters) blends the ground moves by <see cref="Speed"/>, plays <see cref="Pace"/> faster past its fastest
    /// clip, and turns to the air moves by <see cref="Grounded"/> and <see cref="VerticalSpeed"/>; <see cref="TopSpeed"/>
    /// is the fastest clip's speed, which the builder leaves as that parameter's default. With weapons drawn it walks
    /// the drawn ground moves (<see cref="Drawn"/>), and a draw or a sheathe plays on the <see cref="ArmsLayer"/>, weighed
    /// in only while it plays, so the legs move on beneath. A controller built without them just walks. The body moves
    /// itself; the animations only show it (no root motion).
    /// </summary>
    public sealed class HumanoidAnimation
    {
        public const string Speed = "Speed";
        public const string Pace = "Pace";
        public const string TopSpeed = "TopSpeed";
        public const string Grounded = "Grounded";
        public const string VerticalSpeed = "VerticalSpeed";
        public const string Drawn = "Drawn";
        public const string DrawTrigger = "Draw";
        public const string SheatheTrigger = "Sheathe";
        public const string ArmsLayer = "Arms";

        // Seconds the blend takes to catch up with a new speed, so a stop or a start eases rather than snaps.
        const float SpeedDamping = 0.1f;

        // Seconds the arms take to come under a draw or a sheathe, and to go back.
        const float ArmsFade = 0.15f;

        static readonly int SpeedId = Animator.StringToHash(Speed);
        static readonly int PaceId = Animator.StringToHash(Pace);
        static readonly int TopSpeedId = Animator.StringToHash(TopSpeed);
        static readonly int GroundedId = Animator.StringToHash(Grounded);
        static readonly int VerticalSpeedId = Animator.StringToHash(VerticalSpeed);
        static readonly int DrawnId = Animator.StringToHash(Drawn);
        static readonly int DrawId = Animator.StringToHash(DrawTrigger);
        static readonly int SheatheId = Animator.StringToHash(SheatheTrigger);

        readonly Animator _animator;
        readonly float _topSpeed;
        readonly bool _drawnMoves;
        readonly int _arms;
        float _armsWeight;
        float _armsTarget;

        HumanoidAnimation(Animator animator)
        {
            _animator = animator;
            _animator.applyRootMotion = false;
            _topSpeed = animator.GetFloat(TopSpeedId);
            foreach (var parameter in animator.parameters)
                _drawnMoves |= parameter.nameHash == DrawnId;
            _arms = animator.GetLayerIndex(ArmsLayer);
            DrawClip = ClipNamed(animator, DrawTrigger);
            SheatheClip = ClipNamed(animator, SheatheTrigger);
        }

        /// <summary>The clip its draw plays, or null.</summary>
        public AnimationClip DrawClip { get; }

        /// <summary>The clip its sheathe plays, or null.</summary>
        public AnimationClip SheatheClip { get; }

        /// <summary>How long its draw plays, seconds; 0 when it has none (a draw is then at once).</summary>
        public float DrawSeconds => DrawClip != null ? DrawClip.length : 0f;

        /// <summary>How long its sheathe plays, seconds; 0 when it has none.</summary>
        public float SheatheSeconds => SheatheClip != null ? SheatheClip.length : 0f;

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
            FadeArms(deltaTime);
        }

        /// <summary>Shows the weapons becoming <paramref name="readiness"/>: a draw or a sheathe beginning, or done.</summary>
        public void Show(Readiness readiness)
        {
            if (_drawnMoves)
                _animator.SetBool(DrawnId, readiness == Readiness.Drawing || readiness == Readiness.Drawn);
            if (_arms < 0)
                return;
            if (readiness == Readiness.Drawing)
                _animator.SetTrigger(DrawId);
            else if (readiness == Readiness.Sheathing)
                _animator.SetTrigger(SheatheId);
            _armsTarget = readiness == Readiness.Drawing || readiness == Readiness.Sheathing ? 1f : 0f;
        }

        void FadeArms(float deltaTime)
        {
            if (_arms < 0 || _armsWeight == _armsTarget)
                return;
            _armsWeight = deltaTime > 0f ? Mathf.MoveTowards(_armsWeight, _armsTarget, deltaTime / ArmsFade) : _armsTarget;
            _animator.SetLayerWeight(_arms, _armsWeight);
        }

        static AnimationClip ClipNamed(Animator animator, string name)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == name)
                    return clip;
            }

            return null;
        }
    }
}
