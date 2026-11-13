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
    /// Build Characters) blends the ground moves by <see cref="Speed"/> (a set that sidesteps blends its drawn moves by
    /// the way the body goes, <see cref="SideSpeed"/> and <see cref="AheadSpeed"/>), plays <see cref="Pace"/> faster past
    /// its fastest clip, and turns to the air moves by <see cref="Grounded"/> and <see cref="VerticalSpeed"/>;
    /// <see cref="TopSpeed"/> is the fastest clip's speed, which the builder leaves as that parameter's default. With weapons drawn it walks
    /// the crouched ground moves while crouched (<see cref="Crouched"/>, when the set has them), and
    /// the drawn ground moves (<see cref="Drawn"/>), standing in the drawn weapon's style's stance when the set has it
    /// (<see cref="Stance"/>: a bow held low rather than a sword up), and actions (a draw, a sheathe, a slash: <see cref="Play"/>, each
    /// started by a trigger named after its clip) play on an action layer, the <see cref="ArmsLayer"/> (a draw), the
    /// <see cref="UpperBodyLayer"/> (a slash), the <see cref="FullBodyLayer"/> (a death) or, added over all of them, the
    /// <see cref="ReactionsLayer"/> (a blow taken), whichever holds them, weighed in only while they play, so the legs
    /// move on beneath. The weight fades in as an action starts and is out by the time it ends. A controller built without them
    /// just walks. The body moves itself; the animations only show it (no root motion).
    /// </summary>
    public sealed class HumanoidAnimation
    {
        public const string Speed = "Speed";
        /// <summary>Speed to the body's right (negative: left), in the clips' size, for a ground state that blends by the way the body goes.</summary>
        public const string SideSpeed = "SideSpeed";
        /// <summary>Speed ahead of the body (negative: backward), in the clips' size, with <see cref="SideSpeed"/>.</summary>
        public const string AheadSpeed = "AheadSpeed";
        public const string Pace = "Pace";
        public const string TopSpeed = "TopSpeed";
        public const string Grounded = "Grounded";
        public const string VerticalSpeed = "VerticalSpeed";
        public const string Drawn = "Drawn";
        public const string Crouched = "Crouched";
        /// <summary>Which stance it stands in with weapons drawn (<see cref="StanceKey"/>); 0: the set's own drawn moves.</summary>
        public const string Stance = "Stance";
        /// <summary>The move it stands still in, which every set has.</summary>
        public const string IdleMove = "Idle";
        /// <summary>The move it walks in, at its own pace (<see cref="GroundSpeedOf"/>).</summary>
        public const string WalkMove = "Walk";

        /// <summary>The everyday pace's move, whose clip says how fast a character runs.</summary>
        public const string RunMove = "Run";
        /// <summary>The move it sidesteps to the right in (mirrored to the left), at its own pace, when its set has one.</summary>
        public const string StrafeMove = "Strafe";
        /// <summary>The sidestep with its weapons drawn, when its set has one; else the plain one stands in.</summary>
        public const string DrawnStrafeMove = Drawn + StrafeMove;
        /// <summary>The move it stands ready in with its weapons out, when its set has one.</summary>
        public const string DrawnIdleMove = Drawn + IdleMove;
        /// <summary>The move it dies in when its weapon's style has none of its own (<see cref="PlayThrough"/>), when its set has one.</summary>
        public const string DeathMove = "Death";
        /// <summary>The move it gives with when a blow reaches its body and its weapon's style has none of its own, when its set has one.</summary>
        public const string HitMove = "Hit";
        public const string DrawTrigger = "Draw";
        public const string SheatheTrigger = "Sheathe";
        public const string ArmsLayer = "Arms";
        public const string UpperBodyLayer = "Upper Body";
        /// <summary>The action layer of the whole body, whose actions may stay at their end once played (a death).</summary>
        public const string FullBodyLayer = "Full Body";
        /// <summary>The action layer laid over all the others, adding its reactions (a blow taken) to whatever plays beneath.</summary>
        public const string ReactionsLayer = "Reactions";
        /// <summary>An action layer's state while no action plays.</summary>
        public const string RestState = "Rest";

        /// <summary>The pace an action layer's actions play at ("Arms Pace"), 1 as they are, 0 held still (a bow at full draw).</summary>
        public static string ActionPace(string layer) => layer + " Pace";

        /// <summary>The ground state that walks a stance's moves ("BowIdle Ground").</summary>
        public static string StanceGround(string stance) => stance + " Ground";

        /// <summary>
        /// One axis (0 x, 1 y, 2 z) of where the right or left hand's held thing points in the body's frame ("Right Hand
        /// Points Z"), driven by the clips' curves (the import writes them from each move's .json), so the animator blends
        /// it as it blends the moves; the length of the three is how far (<see cref="TryGetHandAim"/>).
        /// </summary>
        public static string HandPoints(bool right, int axis) => (right ? "Right" : "Left") + " Hand Points " + "XYZ"[axis];

        /// <summary>
        /// What <see cref="Stance"/> is set to for <paramref name="stance"/> ("BowIdle"): from its name, so the builder and
        /// the player agree without a list; never 0 (the set's own), and small enough for a transition's threshold to hold
        /// it exactly.
        /// </summary>
        public static int StanceKey(string stance) => (Animator.StringToHash(stance) & 0xFFFF) + 1;

        // Seconds the blend takes to catch up with a new speed, so a stop or a start eases rather than snaps.
        const float SpeedDamping = 0.1f;

        // Seconds an action layer takes to come in as an action starts, and to go out before it ends.
        const float ActionFade = 0.15f;

        // Seconds a reaction blends in over a held action (a shield jolted), and back to it.
        const float ReactBlend = 0.1f;

        static readonly int SpeedId = Animator.StringToHash(Speed);
        static readonly int SideSpeedId = Animator.StringToHash(SideSpeed);
        static readonly int AheadSpeedId = Animator.StringToHash(AheadSpeed);
        static readonly int PaceId = Animator.StringToHash(Pace);
        static readonly int TopSpeedId = Animator.StringToHash(TopSpeed);
        static readonly int GroundedId = Animator.StringToHash(Grounded);
        static readonly int VerticalSpeedId = Animator.StringToHash(VerticalSpeed);
        static readonly int DrawnId = Animator.StringToHash(Drawn);
        static readonly int StanceId = Animator.StringToHash(Stance);
        static readonly int CrouchedId = Animator.StringToHash(Crouched);
        static readonly int RestId = Animator.StringToHash(RestState);

        readonly Animator _animator;
        readonly System.Random _dice = new System.Random();
        readonly float _topSpeed;
        float _frozen;
        readonly bool _crouchedMoves;
        readonly bool _drawnMoves;
        readonly bool _directional;
        readonly bool _stances;
        readonly List<ActionLayer> _actionLayers = new List<ActionLayer>();
        readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        readonly int[] _rightHandPoints;
        readonly int[] _leftHandPoints;

        HumanoidAnimation(Animator animator)
        {
            _animator = animator;
            _animator.applyRootMotion = false;
            _topSpeed = animator.GetFloat(TopSpeedId);
            var side = false;
            var ahead = false;
            foreach (var parameter in animator.parameters)
            {
                _drawnMoves |= parameter.nameHash == DrawnId;
                _crouchedMoves |= parameter.nameHash == CrouchedId;
                _stances |= parameter.nameHash == StanceId;
                side |= parameter.nameHash == SideSpeedId;
                ahead |= parameter.nameHash == AheadSpeedId;
            }

            _directional = side && ahead;
            _rightHandPoints = HandPointsIds(animator, true);
            _leftHandPoints = HandPointsIds(animator, false);
            foreach (var name in new[] { ArmsLayer, UpperBodyLayer, FullBodyLayer, ReactionsLayer })
            {
                var index = animator.GetLayerIndex(name);
                if (index < 0)
                    continue;
                var pace = Animator.StringToHash(ActionPace(name));
                var hasPace = false;
                foreach (var parameter in animator.parameters)
                    hasPace |= parameter.nameHash == pace;
                _actionLayers.Add(new ActionLayer { Name = name, Index = index, Pace = hasPace ? pace : (int?)null, Stays = name == FullBodyLayer });
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

        /// <summary>
        /// Where the moves playing now want what the right (or left) hand holds to point, in the body's frame (x right,
        /// y up, z forward), its length how far they do (0 to 1: blended as the moves are, so a stance's aim fades as it
        /// walks or strikes); false when the controller has no such curves to read. Read once the animator has posed
        /// the frame (<see cref="HandAim"/> turns the hand to it).
        /// </summary>
        public bool TryGetHandAim(bool right, out Vector3 aim)
        {
            var ids = right ? _rightHandPoints : _leftHandPoints;
            aim = default;
            if (ids == null)
                return false;
            aim = new Vector3(_animator.GetFloat(ids[0]), _animator.GetFloat(ids[1]), _animator.GetFloat(ids[2]));
            return true;
        }

        // The hand's three parameters; null when the controller lacks any (built before they were).
        static int[] HandPointsIds(Animator animator, bool right)
        {
            var ids = new int[3];
            for (var axis = 0; axis < 3; axis++)
            {
                ids[axis] = Animator.StringToHash(HandPoints(right, axis));
                var found = false;
                foreach (var parameter in animator.parameters)
                    found |= parameter.nameHash == ids[axis];
                if (!found)
                    return null;
            }

            return ids;
        }

        /// <summary>
        /// The action playing on the upper body now (a raised shield, a slash: <see cref="UpperBodyLayer"/>), how far into
        /// it (a fraction), and how much of it shows in the pose: its layer's weight, less what the whole body's action laid
        /// over it hides (a death); false when none shows. Read once the animator has posed the frame.
        /// </summary>
        public bool TryGetUpperBodyAction(out AnimationClip clip, out float at, out float weight)
        {
            clip = null;
            at = weight = 0f;
            ActionLayer upper = null;
            foreach (var layer in _actionLayers)
            {
                if (layer.Name == UpperBodyLayer)
                    upper = layer;
            }

            if (upper == null || !(upper.Weight > 0f))
                return false;
            var state = _animator.GetCurrentAnimatorStateInfo(upper.Index);
            if (!ClipsByState().TryGetValue(state.shortNameHash, out clip))
                return false;
            at = clip.isLooping ? Mathf.Repeat(state.normalizedTime, 1f) : Mathf.Clamp01(state.normalizedTime);
            weight = upper.Weight;
            foreach (var layer in _actionLayers)
            {
                if (layer.Name == FullBodyLayer)
                    weight *= 1f - layer.Weight;
            }

            return weight > 0f;
        }

        // The controller's clips by the state that plays each (named after it, as the builder names them), made once.
        Dictionary<int, AnimationClip> ClipsByState()
        {
            if (_clipsByState != null)
                return _clipsByState;
            _clipsByState = new Dictionary<int, AnimationClip>();
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                _clipsByState[Animator.StringToHash(clip.name)] = clip;
            return _clipsByState;
        }

        Dictionary<int, AnimationClip> _clipsByState;

        /// <summary>The clip weighing most on the ground (base) layer now; null when none plays.</summary>
        public AnimationClip PlayingGround
        {
            get
            {
                AnimationClip heaviest = null;
                var weight = 0f;
                foreach (var info in _animator.GetCurrentAnimatorClipInfo(0))
                {
                    if (info.weight > weight)
                    {
                        weight = info.weight;
                        heaviest = info.clip;
                    }
                }

                return heaviest;
            }
        }

        /// <summary>
        /// Shows <paramref name="motion"/>, the body's last <paramref name="deltaTime"/> seconds, the body facing
        /// <paramref name="bodyHeading"/> (compass degrees): a ground state that blends by the way the body goes
        /// (<see cref="SideSpeed"/>, <see cref="AheadSpeed"/>) sidesteps and backs away as the body does.
        /// </summary>
        public void Show(BodyMotion motion, float bodyHeading, float deltaTime)
        {
            // Frozen (a blow's impact), nothing comes on, nor does any action's time run out, until the freeze is over.
            if (_frozen > 0f)
            {
                _frozen -= Mathf.Max(0f, deltaTime);
                if (_frozen > 0f)
                    return;
                _frozen = 0f;
                _animator.speed = 1f;
            }

            var stride = StrideMatch.For(motion.GroundSpeed, _animator.humanScale, _topSpeed);
            _animator.SetFloat(SpeedId, stride.BlendSpeed, SpeedDamping, deltaTime);
            if (_directional)
            {
                // The way it goes as the body has it, at the blend's speed (the clips' size, never past the fastest).
                var way = motion.GroundSpeed > 1e-4f
                    ? motion.Relative(bodyHeading) * (stride.BlendSpeed / motion.GroundSpeed)
                    : System.Numerics.Vector2.Zero;
                _animator.SetFloat(SideSpeedId, way.X, SpeedDamping, deltaTime);
                _animator.SetFloat(AheadSpeedId, way.Y, SpeedDamping, deltaTime);
            }

            _animator.SetFloat(PaceId, stride.Pace);
            _animator.SetBool(GroundedId, motion.Grounded);
            _animator.SetFloat(VerticalSpeedId, motion.VerticalSpeed);
            if (_crouchedMoves)
                _animator.SetBool(CrouchedId, motion.Crouched);
            FadeActions(deltaTime);
        }

        /// <summary>
        /// Shows the weapons becoming <paramref name="readiness"/>: a draw or a sheathe beginning (playing
        /// <paramref name="move"/>, a style's, else its own), or done; drawn, it stands in <paramref name="stance"/> (a
        /// style's), when its set has it, else its own drawn moves.
        /// </summary>
        public void Show(Readiness readiness, string move = null, string stance = null)
        {
            if (_stances)
                _animator.SetInteger(StanceId, HasStance(stance) ? StanceKey(stance) : 0);
            if (_drawnMoves)
                _animator.SetBool(DrawnId, readiness == Readiness.Drawing || readiness == Readiness.Drawn);
            if (readiness == Readiness.Drawing)
                Play(move ?? DrawTrigger);
            else if (readiness == Readiness.Sheathing)
                Play(move ?? SheatheTrigger);
        }

        /// <summary>
        /// Plays the action <paramref name="move"/> (a move set's clip beyond its ground and air moves: a draw, a slash, a
        /// blow taken) on the action layer that holds it, weighed in for as long as it lasts, from its start even when it
        /// is playing already (a second blow); false when the set has no such action.
        /// </summary>
        public bool Play(string move)
        {
            if (!TryAction(move, out var layer, out var clip))
                return false;
            var state = Animator.StringToHash(move);
            if (_animator.GetCurrentAnimatorStateInfo(layer.Index).shortNameHash == state)
                _animator.Play(state, layer.Index, 0f);
            else
                _animator.SetTrigger(move);
            SetPace(layer, 1f);
            layer.Held = layer.Thawing = layer.Rebounding = false;
            layer.Reacting = layer.Returning = 0f;
            layer.Left = clip.length;
            return true;
        }

        /// <summary>
        /// Plays the action <paramref name="move"/> through on the whole body and stays at its end (a death: it falls,
        /// and lies as it fell), its layer weighed in, until <see cref="Resume"/> lets it go (at 1: it fades out where it
        /// lies); false when the set has no such action, or it isn't one that stays (a slash plays out).
        /// </summary>
        public bool PlayThrough(string move)
        {
            if (!TryAction(move, out var layer, out var clip) || !layer.Stays)
                return false;
            _animator.SetTrigger(move);
            SetPace(layer, 1f);
            layer.Held = true;
            layer.Thawing = layer.Rebounding = false;
            layer.Reacting = layer.Returning = 0f;
            layer.HeldState = Animator.StringToHash(move);
            layer.HeldAt = HeldShortOfEnd;
            layer.HeldLength = clip.length;
            layer.Left = clip.length;
            return true;
        }

        /// <summary>
        /// Holds the action <paramref name="move"/> still <paramref name="at"/> (a fraction of it: a bow at full draw), its
        /// layer weighed in, until <see cref="Resume"/>; false when the set has no such action, or its layer can't be held.
        /// </summary>
        public bool Hold(string move, float at)
        {
            if (!TryAction(move, out var layer, out var clip) || layer.Pace == null)
                return false;
            // Just short of its end, where it would leave for the layer's rest.
            var held = Mathf.Clamp(at, 0f, HeldShortOfEnd);
            _animator.Play(Animator.StringToHash(move), layer.Index, held);
            SetPace(layer, 0f);
            layer.Held = true;
            layer.Thawing = layer.Rebounding = false;
            layer.Reacting = layer.Returning = 0f;
            layer.HeldState = Animator.StringToHash(move);
            layer.HeldAt = held;
            layer.HeldLength = clip.length;
            return true;
        }

        /// <summary>
        /// Freezes it where it is for <paramref name="seconds"/> (a blow's impact, hit-stop): every layer stops, and the
        /// actions' times with them; a second freeze meanwhile lasts as long as the longer.
        /// </summary>
        public void Freeze(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
                return;
            _frozen = Mathf.Max(_frozen, seconds);
            _animator.speed = 0f;
        }

        /// <summary>
        /// Plays the action <paramref name="move"/> back from where it is (a blade bouncing off what it met) for
        /// <paramref name="seconds"/>, fading out by their end, after which its layer rests; false when it isn't an action
        /// whose pace can be turned (no pace parameter on its layer).
        /// </summary>
        public bool Rebound(string move, float seconds)
        {
            if (!TryAction(move, out var layer, out _) || layer.Pace == null)
                return false;
            SetPace(layer, -1f);
            layer.Held = layer.Thawing = layer.Rebounding = false;
            layer.Reacting = layer.Returning = 0f;
            layer.Left = Mathf.Max(0f, seconds);
            layer.Rebounding = true;
            return true;
        }

        /// <summary>
        /// Plays the action <paramref name="reaction"/> (a shield jolted by a blow) over the held action
        /// <paramref name="held"/> (a raised block), blended in, then blends back to holding that where it was held; false
        /// when it isn't held, or the reaction isn't on its layer.
        /// </summary>
        public bool React(string held, string reaction)
        {
            if (!TryAction(held, out var layer, out _) || !layer.Held || !TryAction(reaction, out var reactionLayer, out var clip) ||
                reactionLayer != layer)
                return false;
            _animator.CrossFadeInFixedTime(Animator.StringToHash(reaction), ReactBlend, layer.Index, 0f);
            SetPace(layer, 1f);
            layer.Reacting = Mathf.Max(ReactBlend, clip.length - ReactBlend);
            layer.Returning = 0f;
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
            layer.Reacting = layer.Returning = 0f;
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

        /// <summary>
        /// Lets the held action <paramref name="held"/> go into the action <paramref name="release"/> (a bow's draw into
        /// its release), blended over <paramref name="blend"/> seconds, starting <paramref name="from"/> (a fraction of the
        /// release) and played out on the same layer; false, and the held one left as it was, when the release isn't on
        /// that layer (or either isn't an action).
        /// </summary>
        public bool FollowThrough(string held, string release, float from, float blend)
        {
            if (!TryAction(held, out var layer, out _) || !layer.Held || !TryAction(release, out var releaseLayer, out var clip) ||
                releaseLayer != layer)
                return false;
            layer.Held = layer.Thawing = layer.Rebounding = false;
            layer.Reacting = layer.Returning = 0f;
            var start = Mathf.Clamp01(from) * clip.length;
            _animator.CrossFadeInFixedTime(Animator.StringToHash(release), Mathf.Max(0f, blend), layer.Index, start);
            SetPace(layer, 1f);
            layer.Left = clip.length - start;
            return true;
        }

        // Whether the set stands in `stance` (the builder gave it a ground state).
        bool HasStance(string stance) =>
            !string.IsNullOrEmpty(stance) && _animator.HasState(0, Animator.StringToHash(StanceGround(stance)));

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

        /// <summary>The name of the <paramref name="number"/>th variant of <paramref name="move"/> ("SwordDeath2"), numbered from 2.</summary>
        public static string Variant(string move, int number) => move + number;

        /// <summary>
        /// The move <paramref name="move"/> or one of its variants, by chance: the set's clips named after it with a number
        /// from 2 on (<see cref="Variant"/>: SwordDeath, SwordDeath2, ...), so a death isn't the same one every time; the
        /// move itself when the set has no variants of it (or not the move).
        /// </summary>
        public string Vary(string move)
        {
            if (Clip(move) == null)
                return move;
            var count = 1;
            while (Clip(Variant(move, count + 1)) != null)
                count++;
            var pick = _dice.Next(count);
            return pick == 0 ? move : Variant(move, pick + 1);
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

        /// <summary>How fast a clip walks: its root motion over the ground, in the clips' own size (the ground moves blend by it).</summary>
        public static float ClipSpeed(AnimationClip clip) => new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude;

        /// <summary>
        /// How fast this body goes over the ground when its ground move <paramref name="move"/> plays at its own pace, its
        /// feet planted: the clip's speed at the body's size, meters per second; 0 without the move.
        /// </summary>
        public float GroundSpeedOf(string move)
        {
            var clip = Clip(move);
            return clip != null ? ClipSpeed(clip) * _animator.humanScale : 0f;
        }

        /// <summary>
        /// How much faster than it was made a stride may play before the legs race (a fast-forward look): a body whose
        /// clips are slower than it goes is held to this much past its fastest (<see cref="DrawnTopSpeeds"/>).
        /// </summary>
        public const float MostStride = 1.2f;

        // The drawn ground moves that walk, slowest first, as the builder names them.
        static readonly string[] DrawnStrides = { Drawn + WalkMove, Drawn + RunMove, Drawn + "Sprint" };

        TopSpeeds? _drawnTopSpeeds;

        /// <summary>
        /// How fast this body goes at most with its weapons drawn, its legs kept to its drawn clips, each played at most
        /// <see cref="MostStride"/> faster at its size; meters per second. Ahead (or back, the same clips played
        /// backward), the fastest of its own drawn ground moves (DrawnRun: Mixamo's sword-and-shield run, a jog); no limit
        /// for a set without drawn ground moves of its own (it walks its plain ones, whose paces the body is given).
        /// Aside, its sidestep (DrawnStrafe, else Strafe: the one the drawn states blend in by the way the body goes);
        /// without one, its drawn moves blend by speed alone and show going aside as they do ahead. Its everyday paces
        /// aren't held: they are the profile's, set against its everyday clips; the drawn stance has no paces but its clips'.
        /// </summary>
        public TopSpeeds DrawnTopSpeeds
        {
            get
            {
                if (!_drawnTopSpeeds.HasValue)
                {
                    var fastest = 0f;
                    foreach (var move in DrawnStrides)
                        fastest = Mathf.Max(fastest, GroundSpeedOf(move));
                    var ahead = AtMostStride(fastest);
                    var sidestep = Clip(DrawnStrafeMove) ?? Clip(StrafeMove);
                    var aside = _directional && sidestep != null ? AtMostStride(ClipSpeed(sidestep) * _animator.humanScale) : ahead;
                    _drawnTopSpeeds = new TopSpeeds(ahead, aside);
                }

                return _drawnTopSpeeds.Value;
            }
        }

        // How fast a stride going `speed` (meters per second) shows at most, played MostStride faster; no limit without one.
        static float AtMostStride(float speed) => speed > 0f ? speed * MostStride : float.PositiveInfinity;

        // Each action layer comes in as its action starts and is out as it ends, so the layer's own state can go back
        // to rest at once, unseen; one held stays in. One let go still fades out still, and plays on once it is out.
        void FadeActions(float deltaTime)
        {
            foreach (var layer in _actionLayers)
            {
                if (!layer.Held)
                    layer.Left = Mathf.Max(0f, layer.Left - Mathf.Max(0f, deltaTime));
                else if (layer.Reacting > 0f || layer.Returning > 0f)
                    React(layer, Mathf.Max(0f, deltaTime));

                var target = layer.Held || layer.Left > ActionFade ? 1f : 0f;
                if (layer.Weight != target)
                {
                    layer.Weight = deltaTime > 0f ? Mathf.MoveTowards(layer.Weight, target, deltaTime / ActionFade) : target;
                    _animator.SetLayerWeight(layer.Index, layer.Weight);
                }

                if ((layer.Thawing || layer.Rebounding) && layer.Weight <= 0f)
                {
                    // A layer that stays never leaves its action by itself, nor does one played back (it runs back to its
                    // start, not on to its end): rested here, unseen, so the next can start.
                    if (layer.Stays || layer.Rebounding)
                        _animator.Play(RestId, layer.Index);
                    layer.Thawing = layer.Rebounding = false;
                    SetPace(layer, 1f);
                }
            }
        }

        // A reaction over a held action plays out, then blends back into the held one, at pace (a state at pace 0 neither
        // takes a time to start at nor lets a blend into it advance), from just short of where it was held so it arrives
        // there as the blend ends; there it is pinned and stilled again.
        void React(ActionLayer layer, float deltaTime)
        {
            if (layer.Reacting > 0f)
            {
                layer.Reacting -= deltaTime;
                if (layer.Reacting > 0f)
                    return;
                layer.Reacting = 0f;
                var arrive = layer.HeldAt * layer.HeldLength;
                _animator.CrossFadeInFixedTime(layer.HeldState, ReactBlend, layer.Index, Mathf.Max(0f, arrive - ReactBlend));
                layer.Returning = ReactBlend;
                return;
            }

            layer.Returning -= deltaTime;
            if (layer.Returning > 0f)
                return;
            layer.Returning = 0f;
            _animator.Play(layer.HeldState, layer.Index, layer.HeldAt);
            SetPace(layer, 0f);
        }

        // How near its end a held action stays (a fraction of it): not at its very end, which it would leave by.
        const float HeldShortOfEnd = 0.995f;

        // An action layer of the controller, how much of it shows, whether its action is held still (by its pace
        // parameter, when the controller has one), and whether its actions stay at their end once played (the whole body's).
        sealed class ActionLayer
        {
            public string Name;
            public int Index;
            public int? Pace;
            public bool Stays;
            public float Weight;
            public float Left;
            public bool Held;
            public bool Thawing;

            // Played back (a blade bouncing off), to be rested once faded out.
            public bool Rebounding;

            // While held: the state held, how far into it (normalized) and its length; and a reaction over it: how long it
            // has left to play, then to blend back.
            public int HeldState;
            public float HeldAt;
            public float HeldLength;
            public float Reacting;
            public float Returning;
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
