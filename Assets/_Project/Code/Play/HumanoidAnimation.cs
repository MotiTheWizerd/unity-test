using System;
using System.Collections.Generic;
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
    /// the drawn ground moves (<see cref="Drawn"/>), and actions (a draw, a sheathe, a slash: <see cref="Play"/>, each
    /// started by a trigger named after its clip) play on the <see cref="ArmsLayer"/>, weighed in only while they play,
    /// so the legs move on beneath. A controller built without them just walks. The body moves itself; the animations
    /// only show it (no root motion).
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

        readonly Animator _animator;
        readonly float _topSpeed;
        readonly bool _drawnMoves;
        readonly int _arms;
        readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        float _armsWeight;
        float _armsLeft;

        HumanoidAnimation(Animator animator)
        {
            _animator = animator;
            _animator.applyRootMotion = false;
            _topSpeed = animator.GetFloat(TopSpeedId);
            foreach (var parameter in animator.parameters)
                _drawnMoves |= parameter.nameHash == DrawnId;
            _arms = animator.GetLayerIndex(ArmsLayer);
            DrawClip = Clip(DrawTrigger);
            SheatheClip = Clip(SheatheTrigger);
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
            if (readiness == Readiness.Drawing)
                Play(DrawTrigger);
            else if (readiness == Readiness.Sheathing)
                Play(SheatheTrigger);
        }

        /// <summary>
        /// Plays the action <paramref name="move"/> (a move set's clip beyond its ground and air moves: a draw, a slash)
        /// on the arms, weighed in for as long as it lasts; false when the set has no such action.
        /// </summary>
        public bool Play(string move)
        {
            var clip = Clip(move);
            if (_arms < 0 || clip == null)
                return false;
            _animator.SetTrigger(move);
            _armsLeft = clip.length;
            return true;
        }

        /// <summary>The clip of <paramref name="move"/> in its controller, or null.</summary>
        public AnimationClip Clip(string move)
        {
            if (string.IsNullOrEmpty(move))
                return null;
            if (!_clips.TryGetValue(move, out var clip))
            {
                clip = ClipNamed(_animator, move);
                _clips.Add(move, clip);
            }

            return clip;
        }

        // The arms come under an action as it starts and go back as it ends.
        void FadeArms(float deltaTime)
        {
            if (_arms < 0)
                return;
            _armsLeft = Mathf.Max(0f, _armsLeft - Mathf.Max(0f, deltaTime));
            var target = _armsLeft > ArmsFade ? 1f : 0f;
            if (_armsWeight == target)
                return;
            _armsWeight = deltaTime > 0f ? Mathf.MoveTowards(_armsWeight, target, deltaTime / ArmsFade) : target;
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
