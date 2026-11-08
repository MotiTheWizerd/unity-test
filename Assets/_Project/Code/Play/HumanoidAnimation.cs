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
    /// started by a trigger named after its clip) play on an action layer, the <see cref="ArmsLayer"/> (a draw) or the
    /// <see cref="UpperBodyLayer"/> (a slash), whichever holds them, weighed in only while they play, so the legs move on
    /// beneath. The weight fades in as an action starts and is out by the time it ends. A controller built without them
    /// just walks. The body moves itself; the animations only show it (no root motion).
    /// </summary>
    public sealed class HumanoidAnimation
    {
        public const string Speed = "Speed";
        public const string Pace = "Pace";
        public const string TopSpeed = "TopSpeed";
        public const string Grounded = "Grounded";
        public const string VerticalSpeed = "VerticalSpeed";
        public const string Drawn = "Drawn";
        /// <summary>The move it stands still in, which every set has.</summary>
        public const string IdleMove = "Idle";
        /// <summary>The move it stands ready in with its weapons out, when its set has one.</summary>
        public const string DrawnIdleMove = Drawn + IdleMove;
        public const string DrawTrigger = "Draw";
        public const string SheatheTrigger = "Sheathe";
        public const string ArmsLayer = "Arms";
        public const string UpperBodyLayer = "Upper Body";

        /// <summary>The pace an action layer's actions play at ("Arms Pace"), 1 as they are, 0 held still (a bow at full draw).</summary>
        public static string ActionPace(string layer) => layer + " Pace";

        // Seconds the blend takes to catch up with a new speed, so a stop or a start eases rather than snaps.
        const float SpeedDamping = 0.1f;

        // Seconds an action layer takes to come in as an action starts, and to go out before it ends.
        const float ActionFade = 0.15f;

        static readonly int SpeedId = Animator.StringToHash(Speed);
        static readonly int PaceId = Animator.StringToHash(Pace);
        static readonly int TopSpeedId = Animator.StringToHash(TopSpeed);
        static readonly int GroundedId = Animator.StringToHash(Grounded);
        static readonly int VerticalSpeedId = Animator.StringToHash(VerticalSpeed);
        static readonly int DrawnId = Animator.StringToHash(Drawn);

        readonly Animator _animator;
        readonly float _topSpeed;
        readonly bool _drawnMoves;
        readonly List<ActionLayer> _actionLayers = new List<ActionLayer>();
        readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);

        HumanoidAnimation(Animator animator)
        {
            _animator = animator;
            _animator.applyRootMotion = false;
            _topSpeed = animator.GetFloat(TopSpeedId);
            foreach (var parameter in animator.parameters)
                _drawnMoves |= parameter.nameHash == DrawnId;
            foreach (var name in new[] { ArmsLayer, UpperBodyLayer })
            {
                var index = animator.GetLayerIndex(name);
                if (index < 0)
                    continue;
                var pace = Animator.StringToHash(ActionPace(name));
                var hasPace = false;
                foreach (var parameter in animator.parameters)
                    hasPace |= parameter.nameHash == pace;
                _actionLayers.Add(new ActionLayer { Index = index, Pace = hasPace ? pace : (int?)null });
            }

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
            FadeActions(deltaTime);
        }

        /// <summary>
        /// Shows the weapons becoming <paramref name="readiness"/>: a draw or a sheathe beginning (playing
        /// <paramref name="move"/>, a style's, else its own), or done.
        /// </summary>
        public void Show(Readiness readiness, string move = null)
        {
            if (_drawnMoves)
                _animator.SetBool(DrawnId, readiness == Readiness.Drawing || readiness == Readiness.Drawn);
            if (readiness == Readiness.Drawing)
                Play(move ?? DrawTrigger);
            else if (readiness == Readiness.Sheathing)
                Play(move ?? SheatheTrigger);
        }

        /// <summary>
        /// Plays the action <paramref name="move"/> (a move set's clip beyond its ground and air moves: a draw, a slash)
        /// on the action layer that holds it, weighed in for as long as it lasts; false when the set has no such action.
        /// </summary>
        public bool Play(string move)
        {
            if (!TryAction(move, out var layer, out var clip))
                return false;
            _animator.SetTrigger(move);
            SetPace(layer, 1f);
            layer.Held = layer.Thawing = false;
            layer.Left = clip.length;
            return true;
        }

        /// <summary>
        /// Holds the action <paramref name="move"/> still <paramref name="at"/> (a fraction of it: a bow at full draw), its
        /// layer weighed in, until <see cref="Resume"/>; false when the set has no such action, or its layer can't be held.
        /// </summary>
        public bool Hold(string move, float at)
        {
            if (!TryAction(move, out var layer, out _) || layer.Pace == null)
                return false;
            // Just short of its end, where it would leave for the layer's rest.
            _animator.Play(Animator.StringToHash(move), layer.Index, Mathf.Clamp(at, 0f, HeldShortOfEnd));
            SetPace(layer, 0f);
            layer.Held = true;
            layer.Thawing = false;
            return true;
        }

        /// <summary>
        /// Lets the held action <paramref name="move"/> play on <paramref name="from"/> (a fraction of it: past a bow's
        /// release); when too little of it is left there to fade out over, it fades out where it is, still.
        /// </summary>
        public void Resume(string move, float from)
        {
            if (!TryAction(move, out var layer, out var clip) || !layer.Held)
                return;
            layer.Held = false;
            var left = (1f - Mathf.Clamp01(from)) * clip.length;
            if (left > ActionFade)
            {
                _animator.Play(Animator.StringToHash(move), layer.Index, Mathf.Clamp01(from));
                SetPace(layer, 1f);
                layer.Left = left;
                return;
            }

            layer.Left = 0f;
            layer.Thawing = true;
        }

        // The action layer that holds `move`, and its clip.
        bool TryAction(string move, out ActionLayer found, out AnimationClip clip)
        {
            found = null;
            clip = Clip(move);
            if (clip == null)
                return false;
            var state = Animator.StringToHash(move);
            foreach (var layer in _actionLayers)
            {
                if (!_animator.HasState(layer.Index, state))
                    continue;
                found = layer;
                return true;
            }

            return false;
        }

        void SetPace(ActionLayer layer, float pace)
        {
            if (layer.Pace.HasValue)
                _animator.SetFloat(layer.Pace.Value, pace);
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

        // Each action layer comes in as its action starts and is out as it ends, so the layer's own state can go back
        // to rest at once, unseen; one held stays in. One let go still fades out still, and plays on once it is out.
        void FadeActions(float deltaTime)
        {
            foreach (var layer in _actionLayers)
            {
                if (!layer.Held)
                    layer.Left = Mathf.Max(0f, layer.Left - Mathf.Max(0f, deltaTime));
                var target = layer.Held || layer.Left > ActionFade ? 1f : 0f;
                if (layer.Weight != target)
                {
                    layer.Weight = deltaTime > 0f ? Mathf.MoveTowards(layer.Weight, target, deltaTime / ActionFade) : target;
                    _animator.SetLayerWeight(layer.Index, layer.Weight);
                }

                if (layer.Thawing && layer.Weight <= 0f)
                {
                    layer.Thawing = false;
                    SetPace(layer, 1f);
                }
            }
        }

        // How near its end a held action stays (a fraction of it): not at its very end, which it would leave by.
        const float HeldShortOfEnd = 0.995f;

        // An action layer of the controller, how much of it shows, and whether its action is held still (by its pace
        // parameter, when the controller has one).
        sealed class ActionLayer
        {
            public int Index;
            public int? Pace;
            public float Weight;
            public float Left;
            public bool Held;
            public bool Thawing;
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
