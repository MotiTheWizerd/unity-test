using System;
using System.Collections.Generic;
using Forge.Game.Combat;
using Forge.Game.Equipment;
using Forge.Game.Motion;
using UnityEngine;
using UnityEngine.Rendering;

namespace Forge.Play
{
    /// <summary>
    /// A character's look riding on its body: the character prefab (Content/Characters) standing on the body's feet (its
    /// soles on the ground: sunk by the body's hover and by how high its idle carries them, <see cref="SoleFinder"/>),
    /// turned to the body's heading and animated as the body moves (<see cref="HumanoidAnimation"/>). The body is the
    /// collider, so the model's own colliders go, and the model shares the body's layer. Whoever has a body uses it
    /// (<see cref="PlayedActor"/>): the player, whose eyes it also carries (<see cref="PlayerEyes"/>), and the characters
    /// of the world. A model without animations just rides along.
    /// <para>
    /// Things it holds or wears hang from its humanoid bones (<see cref="Attach"/>): a hand holds by its grip
    /// (<see cref="GripFrame"/>), worked out from the hand's bones once, so they move with the hand as it animates; any
    /// other bone holds at the bone. What is attached is part of the look: on its layer, without colliders, and, seen from
    /// within (<see cref="FirstPerson"/>), drawn like the bone it hangs from.
    /// </para>
    /// </summary>
    public sealed class CharacterModel : IDisposable
    {
        readonly HumanoidAnimation _animation;
        readonly Animator _humanoid;
        readonly HashSet<Transform> _head;
        readonly Dictionary<HumanBodyBones, Transform> _points = new Dictionary<HumanBodyBones, Transform>();
        readonly Dictionary<HumanBodyBones, Transform> _sheaths = new Dictionary<HumanBodyBones, Transform>();
        readonly Dictionary<GameObject, Held> _attached = new Dictionary<GameObject, Held>();
        readonly GameObject _prefab;
        readonly float _sunk;
        readonly AfterAnimation _afterAnimation;
        HeadlessBody _headless;
        Transform _eyes;
        readonly Dictionary<bool, Transform> _backs = new Dictionary<bool, Transform>();
        bool _firstPerson;
        bool _inHand;

        /// <param name="body">The body object the model rides on.</param>
        /// <param name="hover">How far the body's feet stay above what they stand on (a character controller's skin), meters.</param>
        public CharacterModel(Transform body, GameObject prefab, float hover = 0f)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            _prefab = prefab;
            Root = UnityEngine.Object.Instantiate(prefab, body, false);
            Root.name = prefab.name;
            RemoveColliders(Root);
            SetLayer(Root.transform, body.gameObject.layer);
            Animators = Root.GetComponentsInChildren<Animator>(true);
            foreach (var animator in Animators)
            {
                if (animator.isHuman)
                {
                    _humanoid = animator;
                    break;
                }
            }

            _head = HumanoidHead.Bones(_humanoid);
            _animation = HumanoidAnimation.On(Root);
            // Standing on what the body stands on: down by the body's hover and by how high its idle carries its soles.
            var soles = _animation != null && SoleFinder.TryFind(prefab, IdleClip, out var height) ? height : 0f;
            _sunk = hover + soles;
            Root.transform.localPosition = Vector3.down * _sunk;
            _afterAnimation = Root.AddComponent<AfterAnimation>();
            // The hands first: what they hold is laid on them after.
            _afterAnimation.Posed += AimHands;
            _afterAnimation.Posed += FaceHolders;
        }

        /// <summary>
        /// Every frame once its animations have posed it (<see cref="AfterAnimation"/>), for what works on the pose:
        /// turning it (<see cref="TurnUpperBody"/>), laying something along it.
        /// </summary>
        public event Action Posed
        {
            add => _afterAnimation.Posed += value;
            remove => _afterAnimation.Posed -= value;
        }

        /// <summary>
        /// <paramref name="prefab"/>'s look standing still (its idle) under <paramref name="parent"/>, for a picture of the
        /// character (the inventory's); destroying the object returned is all it takes to take it away, and what is
        /// attached to it goes with it.
        /// </summary>
        public static CharacterModel StandingStill(Transform parent, GameObject prefab)
        {
            var model = new CharacterModel(parent, prefab);
            model.Animate(BodyMotion.Still, 0f);
            return model;
        }

        /// <summary>The model's object, a child of the body.</summary>
        public GameObject Root { get; }

        public Animator[] Animators { get; }

        /// <summary>Its humanoid's <paramref name="bone"/> (a foot, the head), or null when it has no humanoid or no such bone.</summary>
        public Transform Bone(HumanBodyBones bone) => _humanoid != null ? _humanoid.GetBoneTransform(bone) : null;

        /// <summary>
        /// Seen from inside its own head (first person): all of it draws but its head, which, with what hangs from it,
        /// casts only its shadow (<see cref="HeadlessBody"/>); what it holds draws as ever, and its shadow is whole.
        /// </summary>
        public bool FirstPerson
        {
            get => _firstPerson;
            set
            {
                if (_firstPerson == value)
                    return;
                _firstPerson = value;
                if (_headless == null && _head.Count > 0)
                    _headless = new HeadlessBody(Root, _humanoid.GetBoneTransform(HumanBodyBones.Head), _head);
                _headless?.Show(value);
                foreach (var held in _attached.Values)
                    Draw(held);
            }
        }

        /// <summary>
        /// A point riding its head where its eyes are, made the first time it is asked for
        /// (<see cref="HumanoidHead.TryFindEyes"/>): <paramref name="height"/> above its body's feet as it stands at rest,
        /// at the front of its face; so it sways, leans and stoops with the head as it animates. Null without a head.
        /// </summary>
        public Transform Eyes(float height)
        {
            if (_eyes != null)
                return _eyes;
            var head = _humanoid != null ? _humanoid.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null || !HumanoidHead.TryFindEyes(_prefab, IdleClip, height + _sunk, out var inHead))
                return null;
            _eyes = new GameObject("Eyes").transform;
            _eyes.SetParent(head, false);
            _eyes.localPosition = inHead;
            _eyes.gameObject.layer = Root.layer;
            return _eyes;
        }

        /// <summary>
        /// How far above its body's feet its eyes are as it stands in its idle, meters, read off its own head
        /// (<see cref="HumanoidHead.TryFindEyeLevel"/>), which <see cref="Eyes"/> takes; false without a head or an idle.
        /// </summary>
        public bool TryGetEyeHeight(out float height)
        {
            height = HumanoidHead.TryFindEyeLevel(_prefab, IdleClip, out var aboveRoot) ? aboveRoot - _sunk : 0f;
            return height > 0f;
        }

        AnimationClip IdleClip => _animation?.Clip(HumanoidAnimation.IdleMove);

        /// <summary>The clip it stands ready in, holding its weapons out: its drawn idle, else its idle; null without animations.</summary>
        public AnimationClip StanceClip => _animation?.Clip(HumanoidAnimation.DrawnIdleMove) ?? IdleClip;

        /// <summary>Turns the model to <paramref name="heading"/> (compass degrees).</summary>
        public void Face(float heading) => Root.transform.localRotation = Quaternion.Euler(0f, heading, 0f);

        /// <summary>Shows how the body moved over the last <paramref name="deltaTime"/> seconds (standing, walking, a jump).</summary>
        public void Animate(BodyMotion motion, float deltaTime) => _animation?.Show(motion, deltaTime);

        /// <summary>
        /// Shows its weapons becoming <paramref name="readiness"/> (a draw or a sheathe playing, or done); a draw or a
        /// sheathe plays <paramref name="move"/> (a style's: "BowDraw"), else its own; drawn, it stands in
        /// <paramref name="stance"/> (a style's: "BowIdle"), else its own drawn stance.
        /// </summary>
        public void Animate(Readiness readiness, string move = null, string stance = null) => _animation?.Show(readiness, move, stance);

        /// <summary>
        /// How its draw or sheathe <paramref name="move"/> plays: the clip's length, and the moment in it the hand is at
        /// where what it draws is carried, on the back (<see cref="BackFinder"/>) or at the hip (<see cref="SheathFinder"/>);
        /// false when it has no such move.
        /// </summary>
        public bool TryReadyTime(string move, out ReadyTiming timing)
        {
            timing = default;
            var clip = _animation?.Clip(move);
            if (clip == null)
                return false;
            var moment = 1f;
            var onBack = false;
            foreach (var held in _attached.Values)
            {
                if (held.Sheathes && held.Carried == CarriedAt.Back &&
                    BackFinder.TryFindReach(_prefab, clip, IdleClip, held.Bone == HumanBodyBones.RightHand, out moment))
                {
                    onBack = true;
                    break;
                }
            }

            if (!onBack)
                moment = SheathFinder.TryFind(_prefab, clip, out var sheaths) ? sheaths.Moment : 1f;
            timing = new ReadyTiming(clip.length, moment);
            return true;
        }

        /// <summary>Plays the action <paramref name="move"/> (a slash) on its arms; false when it has no such move.</summary>
        public bool Play(string move) => _animation != null && _animation.Play(move);

        /// <summary>
        /// Plays the action <paramref name="move"/> through on its whole body and stays at its end (a death: it lies as it
        /// fell) until <see cref="Resume"/> at 1 lets it go; false when it has no such move, or it isn't one that stays.
        /// </summary>
        public bool PlayThrough(string move) => _animation != null && _animation.PlayThrough(move);

        /// <summary>
        /// How its move <paramref name="move"/> plays, measured from the clip as <paramref name="measure"/> says: a bow's
        /// draw by the string's pull (<see cref="ShotFinder"/>), a block by the shield coming up (<see cref="RaiseFinder"/>),
        /// else by the blade's cut (<see cref="SwingFinder"/>); false when it has none.
        /// </summary>
        public bool TryTime(string move, MoveMeasure measure, out MoveTiming timing)
        {
            timing = default;
            if (_animation == null)
                return false;
            var clip = _animation.Clip(move);
            switch (measure)
            {
                case MoveMeasure.Draw: return ShotFinder.TryFind(_prefab, clip, out timing);
                case MoveMeasure.Raise: return RaiseFinder.TryFind(_prefab, clip, out timing);
                default: return SwingFinder.TryFind(_prefab, clip, out timing);
            }
        }

        /// <summary>
        /// Plays the action <paramref name="reaction"/> (a shield jolted) over its held action <paramref name="held"/> (a
        /// raised block), then holds that again; false when it can't (not where the held one plays).
        /// </summary>
        public bool React(string held, string reaction) => _animation != null && _animation.React(held, reaction);

        /// <summary>Holds its action <paramref name="move"/> still <paramref name="at"/> (a fraction of it: a bow at full draw).</summary>
        public bool Hold(string move, float at) => _animation != null && _animation.Hold(move, at);

        /// <summary>Lets its held action <paramref name="move"/> play on <paramref name="from"/> (a fraction of it).</summary>
        public void Resume(string move, float from) => _animation?.Resume(move, from);

        /// <summary>
        /// How long its move <paramref name="release"/> plays as a held move's release (a bow's follow-through), seconds,
        /// from where it starts (<see cref="ReleaseFinder"/>) to its end; false when it has none.
        /// </summary>
        public bool TryTimeRelease(string release, out float seconds)
        {
            seconds = 0f;
            var clip = _animation?.Clip(release);
            if (clip == null || !ReleaseFinder.TryFind(_prefab, clip, out var from))
                return false;
            seconds = (1f - from) * clip.length;
            return true;
        }

        /// <summary>
        /// Lets its held action <paramref name="held"/> go into <paramref name="release"/> (a bow's draw into its
        /// release), from where the release starts (<see cref="ReleaseFinder"/>); false when it can't (no such release,
        /// or not where the held one plays), and the held one is left as it was.
        /// </summary>
        public bool Release(string held, string release)
        {
            var clip = _animation?.Clip(release);
            return clip != null && ReleaseFinder.TryFind(_prefab, clip, out var from) &&
                _animation.FollowThrough(held, release, from, ReleaseFinder.Lead);
        }

        /// <summary>
        /// How far into its draw <paramref name="move"/> (a bow's) the right hand is nearest the back over the right
        /// shoulder, 0 to 1: where it takes an arrow from the quiver (<see cref="BackFinder"/>); false when it can't tell.
        /// </summary>
        public bool TryArrowTaken(string move, out float moment)
        {
            moment = 0f;
            return _animation != null && BackFinder.TryFindReach(_prefab, _animation.Clip(move), IdleClip, true, out moment);
        }

        /// <summary>Where the right or left hand grips (it moves with the hand); false without the hand.</summary>
        public bool TryGrip(bool right, out Transform grip) => TryPoint(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand, out grip);

        /// <summary>
        /// Turns its upper body by <paramref name="turn"/> (scene space), spread evenly over the spine's bones, so what
        /// the arms hold turns by all of it while the hips stay: an archer swinging the bow onto the aim, high or low, left
        /// or right. On this frame's pose, so from <see cref="Posed"/>.
        /// </summary>
        public void TurnUpperBody(Quaternion turn)
        {
            if (_humanoid == null)
                return;
            var bones = 0;
            foreach (var id in SpineBones)
                bones += _humanoid.GetBoneTransform(id) != null ? 1 : 0;
            if (bones == 0)
                return;
            // Each bone turns by its share about its own joint; one after the other, what hangs from the last turns by all.
            var share = Quaternion.Slerp(Quaternion.identity, turn, 1f / bones);
            foreach (var id in SpineBones)
            {
                var bone = _humanoid.GetBoneTransform(id);
                if (bone != null)
                    bone.rotation = share * bone.rotation;
            }
        }

        static readonly HumanBodyBones[] SpineBones = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest };

        /// <summary>
        /// Makes <paramref name="look"/> part of its look without hanging it anywhere (an arrow on the string, laid there by
        /// whoever holds it): under its root, on its layer, without colliders.
        /// </summary>
        public void Adopt(GameObject look)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));
            look.transform.SetParent(Root.transform, true);
            RemoveColliders(look);
            SetLayer(look.transform, Root.layer);
        }

        /// <summary>How long its draw plays, seconds; 0 without one.</summary>
        public float DrawSeconds => _animation?.DrawSeconds ?? 0f;

        /// <summary>How long its sheathe plays, seconds; 0 without one.</summary>
        public float SheatheSeconds => _animation?.SheatheSeconds ?? 0f;

        /// <summary>How far into its draw the hand takes the hilt (when the weapon leaves the sheath), 0 to 1; 1 when unknown.</summary>
        public float DrawnAt => SheathFinder.TryFind(_prefab, _animation?.DrawClip, out var sheaths) ? sheaths.Moment : 1f;

        /// <summary>How far into its sheathe the hand lets go at the sheath, 0 to 1; 1 when unknown.</summary>
        public float SheathedAt => SheathFinder.TryFind(_prefab, _animation?.SheatheClip, out var sheaths) ? sheaths.Moment : 1f;

        /// <summary>
        /// Whether what sheathes is in hand (weapons drawn) or in its sheath at the hip, where the model's draw clip takes it
        /// from (<see cref="SheathFinder"/>); a model that can't tell keeps it in hand.
        /// </summary>
        public bool InHand
        {
            get => _inHand;
            set
            {
                if (_inHand == value)
                    return;
                _inHand = value;
                foreach (var held in _attached.Values)
                {
                    if (held.Sheathes)
                        Place(held);
                }

                PlaceWornOnBack();
            }
        }

        /// <summary>
        /// Hangs <paramref name="look"/> from the humanoid bone named <paramref name="bone"/> ("RightHand"), held by
        /// <paramref name="grip"/> (a part of it, or itself): the grip's pivot and axes on the bone's attach point; or, when
        /// it <paramref name="sheathes"/> and is not <see cref="InHand"/>, where it is carried meanwhile: the hand's sheath
        /// at the hip, or across the back (<see cref="BackFinder"/>) when <paramref name="how"/> says so. A thing whose
        /// <paramref name="how"/> names a side that faces its holder (a bow's string) is turned about its length: in the
        /// hand to face the chest, every frame, whatever pose the hand is in (<see cref="FaceHolders"/>), or, strapped to
        /// the forearm (a shield), laid along it once with that side to the hand (<see cref="HoldFinder.Strapped"/>); on
        /// the back it lies flat, that side down across the back, standing just off it. False, and the look destroyed,
        /// when the model has no such bone.
        /// </summary>
        public bool Attach(GameObject look, Transform grip, string bone, bool sheathes = false, HeldLook how = null)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));
            if (!Enum.TryParse(bone, out HumanBodyBones id) || !TryPoint(id, out _))
            {
                Discard(look);
                return false;
            }

            var held = new Held
            {
                Look = look,
                Grip = grip,
                Bone = id,
                Sheathes = sheathes,
                Carried = how != null ? how.Carried : CarriedAt.Hip,
                Size = look.transform.localScale,
                Renderers = look.GetComponentsInChildren<Renderer>(true),
                FacesHolder = how != null ? how.FacesHolder : null,
                InHandTurn = Quaternion.identity,
                InHand = how != null ? how.InHand : Pose.identity,
            };
            if (how != null && how.AlongArm.HasValue && how.FacesHolder.HasValue && IsHand(id))
            {
                held.Strapped = true;
                held.InHandTurn = HoldFinder.Strapped(how.FacesHolder.Value, how.AlongArm.Value, id == HumanBodyBones.RightHand);
            }
            held.Reach = ExtentOf(held, Vector3.up);
            RemoveColliders(look);
            SetLayer(look.transform, Root.layer);
            _attached.Add(look, held);
            Draw(held);
            Place(held);
            PlaceWornOnBack();
            return true;
        }

        /// <summary>Takes <paramref name="look"/> off and destroys it.</summary>
        public void Detach(GameObject look)
        {
            if (look == null || !_attached.TryGetValue(look, out var held))
                return;
            _attached.Remove(look);
            Discard(look);
            PlaceWornOnBack();
        }

        /// <summary>
        /// Where the blade of what <paramref name="bone"/> ("RightHand") holds is now, in the hand (scene space): from the
        /// grip out along the held thing's +Y to its farthest point; false when the bone holds nothing, or it is sheathed.
        /// </summary>
        public bool TryGetBlade(string bone, out Vector3 from, out Vector3 to)
        {
            from = to = default;
            if (!Enum.TryParse(bone, out HumanBodyBones id))
                return false;
            foreach (var held in _attached.Values)
            {
                if (held.Bone != id || (held.Sheathes && !_inHand) || held.Reach <= 0f)
                    continue;
                from = held.Grip.position;
                to = from + held.Grip.up * (held.Reach * held.Grip.lossyScale.y);
                return true;
            }

            return false;
        }

        /// <summary>The look <paramref name="bone"/> ("RightHand") holds in hand now, and its grip; false when it holds nothing, or it is sheathed.</summary>
        public bool TryGetHeld(string bone, out GameObject look, out Transform grip)
        {
            look = null;
            grip = null;
            if (!Enum.TryParse(bone, out HumanBodyBones id))
                return false;
            foreach (var held in _attached.Values)
            {
                if (held.Bone != id || (held.Sheathes && !_inHand))
                    continue;
                look = held.Look;
                grip = held.Grip;
                return true;
            }

            return false;
        }

        /// <summary>The ground move it plays most of now (its idle, its drawn idle), for whatever tunes a move by its look; null without animations.</summary>
        public AnimationClip PlayingMove => _animation?.PlayingGround;

        // How far a held thing reaches from its grip along the grip's `axis` (+Y: out of the fist), in the grip's own
        // units: the farthest corner of its meshes' bounds; zero when nothing of it lies that way.
        static float ExtentOf(Held held, Vector3 axis)
        {
            var reach = 0f;
            foreach (var renderer in held.Renderers)
            {
                var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;
                var bounds = mesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var inGrip = held.Grip.InverseTransformPoint(renderer.transform.TransformPoint(local));
                    reach = Mathf.Max(reach, Vector3.Dot(inGrip, axis));
                }
            }

            return reach;
        }

        // In the hand; or, put away, where it is carried when the model knows where that is: its sheath, or on the back.
        // What no hand holds and is carried on the back (a quiver) is always there, over the right shoulder, the hand
        // that draws from it: crossing a bow on the back (over the left), on top of it.
        void Place(Held held)
        {
            var point = _points[held.Bone];
            var turn = held.InHandTurn;
            var seat = held.InHand;
            var hand = IsHand(held.Bone);
            held.OnBack = false;
            if ((held.Sheathes && !_inHand) || (!hand && held.Carried == CarriedAt.Back))
            {
                if (held.Carried == CarriedAt.Back && TryBack(held.Bone != HumanBodyBones.LeftHand, out var back))
                {
                    // Flat against the back: its holder's side down across it, away from the neck; or, strapped (a
                    // shield), that side against the back, upright, slung by its middle.
                    point = back;
                    turn = held.Strapped ? HoldFinder.Against(held.FacesHolder.Value, back.InverseTransformDirection(Vector3.up))
                        : held.FacesHolder.HasValue ? HoldFinder.Facing(held.FacesHolder.Value, back.InverseTransformDirection(Vector3.down))
                        : Quaternion.identity;
                    seat = Pose.identity;
                    held.OnBack = true;
                }
                else if (TrySheath(held.Bone, out var sheath))
                {
                    point = sheath;
                    turn = Quaternion.identity;
                    seat = Pose.identity;
                }
            }

            // Its own size whatever the skeleton's scale, then turned (about its length too, so its holder's side faces
            // the way the place wants) and moved so that its grip lies on the point, or off the back by its depth (and on
            // top of what a hand carries there, for what no hand holds), a strapped thing's middle over the point.
            var look = held.Look.transform;
            look.SetParent(point, false);
            var boneScale = point.lossyScale.x;
            look.localScale = boneScale > 0f ? held.Size / boneScale : held.Size;
            var gripTurn = Quaternion.Inverse(look.rotation) * held.Grip.rotation;
            look.rotation = point.rotation * turn * seat.rotation * Quaternion.Inverse(gripTurn);
            var scale = held.Grip.lossyScale.z;
            held.Thickness = held.OnBack
                ? (ExtentOf(held, Quaternion.Inverse(turn) * Vector3.back) + ExtentOf(held, Quaternion.Inverse(turn) * Vector3.forward)) * scale
                : 0f;
            var standOff = held.OnBack
                ? BackFinder.Strap + (hand ? 0f : HandCarriedOnBack()) + ExtentOf(held, Quaternion.Inverse(turn) * Vector3.back) * scale
                : 0f;
            var hangsBy = held.Grip.position;
            if (held.OnBack && held.Strapped)
                hangsBy += Vector3.ProjectOnPlane(held.Grip.TransformVector(MiddleOf(held)), point.forward);
            look.position += point.position + point.forward * standOff + SeatOffset(held, point, turn, seat) - hangsBy;
        }

        // How far a held thing's grip sits off the point it is held at, seated as its look says (HeldLook.InHand): its
        // offset in the grip's frame as the point turns it, at the grip's scale.
        static Vector3 SeatOffset(Held held, Transform point, Quaternion turn, Pose seat) =>
            point.rotation * turn * (seat.position * held.Grip.lossyScale.x);

        // The middle of what a held thing draws, from its grip, in the grip's own frame: halfway between its farthest
        // reaches each way along each axis.
        static Vector3 MiddleOf(Held held)
        {
            var middle = Vector3.zero;
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                middle += axis * ((ExtentOf(held, axis) - ExtentOf(held, -axis)) * 0.5f);
            return middle;
        }

        // Each hand turned, on this frame's pose, the least that points what it holds (its grip's +Y: the blade) where
        // the moves playing want it, as far as they do (HumanoidAnimation.TryGetHandAim, from the moves' .json beside
        // their clips): a Mixamo move is animated with nothing in the hand, and holds our sword as it may. The fingers,
        // the grip and what hangs from it come along, so it turns an empty hand just the same.
        void AimHands()
        {
            if (_animation == null)
                return;
            AimHand(true);
            AimHand(false);
        }

        void AimHand(bool right)
        {
            if (!_animation.TryGetHandAim(right, out var aim))
                return;
            // Eased, so the turn comes over a moment as the moves hand over (a draw ending) rather than in a frame.
            ref var eased = ref (right ? ref _rightAim : ref _leftAim);
            eased = Vector3.MoveTowards(eased, aim, Time.deltaTime / AimEase);
            AimHand(right, eased);
        }

        /// <summary>
        /// Turns the right or left hand, on its pose as it stands now, the least that points what it holds (its grip's +Y)
        /// along <paramref name="aim"/> in the body's frame, by as much as its length (<see cref="HandAim"/>); nothing for
        /// no aim. What the moves playing want is applied each frame by itself; this is for a pose held still (the
        /// editor's hold stage).
        /// </summary>
        public void AimHand(bool right, Vector3 aim)
        {
            if (aim == Vector3.zero)
                return;
            var id = right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
            if (!TryPoint(id, out var point))
                return;
            var hand = _humanoid.GetBoneTransform(id);
            var turn = HandAim.Turn(point.up, Root.transform.TransformDirection(aim));
            // The forearm rolls its share about its length (the wrist stays put); the hand turns the rest.
            var forearm = _humanoid.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            if (forearm != null)
            {
                var share = HandAim.ForearmPart(turn, hand.position - forearm.position);
                forearm.rotation = share * forearm.rotation;
                turn *= Quaternion.Inverse(share);
            }

            hand.rotation = turn * hand.rotation;
        }

        // Seconds a hand's aim takes to come all the way in or out.
        const float AimEase = 0.35f;
        Vector3 _rightAim;
        Vector3 _leftAim;

        // What a hand holds with a side that faces its holder (a bow's string) turns about its length, on this frame's
        // pose, to face the chest: the stance holds the bow one way, a draw another, and the string faces the archer in both.
        void FaceHolders()
        {
            var chest = BackFinder.Chest(_humanoid);
            if (chest == null)
                return;
            foreach (var held in _attached.Values)
            {
                if (!held.FacesHolder.HasValue || held.Strapped || held.OnBack || !IsHand(held.Bone) || (held.Sheathes && !_inHand))
                    continue;
                var point = _points[held.Bone];
                var toward = Quaternion.Inverse(point.rotation) * (chest.position - point.position);
                held.InHandTurn = HoldFinder.Facing(held.FacesHolder.Value, toward);
                var look = held.Look.transform;
                var gripTurn = Quaternion.Inverse(look.rotation) * held.Grip.rotation;
                look.rotation = point.rotation * held.InHandTurn * held.InHand.rotation * Quaternion.Inverse(gripTurn);
                look.position += point.position + SeatOffset(held, point, held.InHandTurn, held.InHand) - held.Grip.position;
            }
        }

        // How thick what the hands carry on the back is there now (with its strap), which what no hand holds lies on.
        float HandCarriedOnBack()
        {
            var thickest = 0f;
            foreach (var held in _attached.Values)
            {
                if (held.OnBack && IsHand(held.Bone))
                    thickest = Mathf.Max(thickest, held.Thickness + BackFinder.Strap);
            }

            return thickest;
        }

        // What no hand holds and is worn on the back, placed again on what the hands carry there now.
        void PlaceWornOnBack()
        {
            foreach (var held in _attached.Values)
            {
                if (!IsHand(held.Bone) && held.Carried == CarriedAt.Back)
                    Place(held);
            }
        }

        // The spot across the back over the right or the left shoulder, made the first time it is asked for, from
        // where the idle carries the back.
        bool TryBack(bool overRight, out Transform back)
        {
            if (_backs.TryGetValue(overRight, out back))
                return true;
            var chest = BackFinder.Chest(_humanoid);
            if (chest == null || !BackFinder.TryFind(_prefab, IdleClip, overRight, out var pose))
                return false;

            back = new GameObject(overRight ? "Back (right shoulder)" : "Back (left shoulder)").transform;
            back.SetParent(chest, false);
            back.gameObject.layer = Root.layer;
            back.SetPositionAndRotation(chest.position + chest.rotation * pose.position, chest.rotation * pose.rotation);
            _backs.Add(overRight, back);
            return true;
        }

        static bool IsHand(HumanBodyBones bone) => bone == HumanBodyBones.RightHand || bone == HumanBodyBones.LeftHand;

        // A hand's sheath at the hip, made the first time it is asked for, from where the draw clip takes the hilt.
        bool TrySheath(HumanBodyBones hand, out Transform sheath)
        {
            sheath = null;
            if (!IsHand(hand))
                return false;
            if (_sheaths.TryGetValue(hand, out sheath))
                return true;
            var hips = _humanoid != null ? _humanoid.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips == null || !SheathFinder.TryFind(_prefab, _animation?.DrawClip, out var sheaths))
                return false;

            var pose = sheaths.Of(hand == HumanBodyBones.RightHand);
            sheath = new GameObject("Sheath " + hand).transform;
            sheath.SetParent(hips, false);
            sheath.gameObject.layer = Root.layer;
            sheath.SetPositionAndRotation(hips.position + hips.rotation * pose.position, hips.rotation * pose.rotation);
            _sheaths.Add(hand, sheath);
            return true;
        }

        public void Dispose()
        {
            if (Root != null)
                Discard(Root);
        }

        // Gone at the end of the frame while playing; at once in the editor, where a deferred destroy is refused
        // (the editor's hold stage builds a model outside play).
        static void Discard(UnityEngine.Object thing)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(thing);
            else
                UnityEngine.Object.DestroyImmediate(thing);
        }

        // A bone's attach point, made the first time it is asked for: a hand's grip, any other bone itself.
        bool TryPoint(HumanBodyBones id, out Transform point)
        {
            point = null;
            if (_humanoid == null || id < 0 || id >= HumanBodyBones.LastBone)
                return false;
            if (_points.TryGetValue(id, out point))
                return true;
            var boneTransform = _humanoid.GetBoneTransform(id);
            if (boneTransform == null)
                return false;

            point = new GameObject("Holds " + id).transform;
            point.SetParent(boneTransform, false);
            point.gameObject.layer = Root.layer;
            if ((id == HumanBodyBones.RightHand || id == HumanBodyBones.LeftHand) &&
                HumanoidHands.TryGrip(_humanoid, id == HumanBodyBones.RightHand, out var grip))
                point.SetPositionAndRotation(grip.position, grip.rotation);
            _points.Add(id, point);
            return true;
        }

        // A thing hung from the model: how it is held, where, and whether it goes to a sheath.
        sealed class Held
        {
            public GameObject Look;
            public Transform Grip;
            public HumanBodyBones Bone;
            public bool Sheathes;
            public Vector3 Size;
            public Renderer[] Renderers;
            public CarriedAt Carried;
            public float Reach;

            // The side that faces its holder (a bow's string), in the grip's frame; and the turn about its length that
            // brings it round to the body in the hand, or, strapped to the forearm (a shield), the one turn that lays it
            // along the arm, that side to the hand.
            public Vector3? FacesHolder;
            public bool Strapped;
            public Quaternion InHandTurn;

            // How the model sits on the grip in the hand, after that turn (HeldLook.InHand).
            public Pose InHand;

            // Whether it is on the back now, and how thick it lies there (meters).
            public bool OnBack;
            public float Thickness;
        }

        // Drawn like the bone it hangs from: seen from within, only the shadow of what the head wears.
        void Draw(Held held)
        {
            var bone = _humanoid.GetBoneTransform(held.Bone);
            var mode = _firstPerson && bone != null && bone.HangsFrom(_head) ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var renderer in held.Renderers)
                renderer.shadowCastingMode = mode;
        }

        static void RemoveColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Discard(collider);
            }
        }

        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform)
                SetLayer(child, layer);
        }
    }
}
