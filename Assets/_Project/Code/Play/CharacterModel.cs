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
    /// collider, so the model's own colliders go, and the model shares the body's layer. Whoever has a body uses it: the
    /// player (<see cref="PlayerAvatar"/>), and characters of the world later. A model without animations just rides along.
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
        HeadlessBody _headless;
        Transform _eyes;
        readonly Dictionary<HumanBodyBones, Transform> _backs = new Dictionary<HumanBodyBones, Transform>();
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

        AnimationClip IdleClip => _animation?.Clip(HumanoidAnimation.IdleMove);

        /// <summary>The clip it stands ready in, holding its weapons out: its drawn idle, else its idle; null without animations.</summary>
        public AnimationClip StanceClip => _animation?.Clip(HumanoidAnimation.DrawnIdleMove) ?? IdleClip;

        /// <summary>Turns the model to <paramref name="heading"/> (compass degrees).</summary>
        public void Face(float heading) => Root.transform.localRotation = Quaternion.Euler(0f, heading, 0f);

        /// <summary>Shows how the body moved over the last <paramref name="deltaTime"/> seconds (standing, walking, a jump).</summary>
        public void Animate(BodyMotion motion, float deltaTime) => _animation?.Show(motion, deltaTime);

        /// <summary>
        /// Shows its weapons becoming <paramref name="readiness"/> (a draw or a sheathe playing, or done); a draw or a
        /// sheathe plays <paramref name="move"/> (a style's: "BowDraw"), else its own.
        /// </summary>
        public void Animate(Readiness readiness, string move = null) => _animation?.Show(readiness, move);

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

        /// <summary>How its move <paramref name="move"/> plays, measured from the clip (<see cref="SwingFinder"/>); false when it has none.</summary>
        public bool TryTime(string move, out MoveTiming timing)
        {
            timing = default;
            return _animation != null && SwingFinder.TryFind(_prefab, _animation.Clip(move), out timing);
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
            }
        }

        /// <summary>
        /// Hangs <paramref name="look"/> from the humanoid bone named <paramref name="bone"/> ("RightHand"), held by
        /// <paramref name="grip"/> (a part of it, or itself): the grip's pivot and axes on the bone's attach point; or, when
        /// it <paramref name="sheathes"/> and is not <see cref="InHand"/>, where it is carried meanwhile: the hand's sheath
        /// at the hip, or across the back (<see cref="BackFinder"/>) when <paramref name="how"/> says so. A thing whose
        /// <paramref name="how"/> names a side that faces its holder (a bow's string) is turned about its length: in the
        /// hand to face the body as it stands ready (<see cref="HoldFinder"/>); on the back it lies flat, that side down
        /// across the back, standing just off it. False, and the look destroyed, when the model has no such bone.
        /// </summary>
        public bool Attach(GameObject look, Transform grip, string bone, bool sheathes = false, HeldLook how = null)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));
            if (!Enum.TryParse(bone, out HumanBodyBones id) || !TryPoint(id, out _))
            {
                UnityEngine.Object.Destroy(look);
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
            };
            if (held.FacesHolder.HasValue && IsHand(id) && HoldFinder.TryFindBody(_prefab, StanceClip, id == HumanBodyBones.RightHand, out var body))
                held.InHandTurn = HoldFinder.Facing(held.FacesHolder.Value, body);
            held.Reach = ExtentOf(held, Vector3.up);
            RemoveColliders(look);
            SetLayer(look.transform, Root.layer);
            _attached.Add(look, held);
            Draw(held);
            Place(held);
            return true;
        }

        /// <summary>Takes <paramref name="look"/> off and destroys it.</summary>
        public void Detach(GameObject look)
        {
            if (look == null || !_attached.TryGetValue(look, out var held))
                return;
            _attached.Remove(look);
            UnityEngine.Object.Destroy(look);
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
        void Place(Held held)
        {
            var point = _points[held.Bone];
            var turn = held.InHandTurn;
            var onBack = false;
            if (held.Sheathes && !_inHand)
            {
                if (held.Carried == CarriedAt.Back && TryBack(held.Bone, out var back))
                {
                    // Flat against the back, its holder's side down across it, away from the neck.
                    point = back;
                    turn = held.FacesHolder.HasValue
                        ? HoldFinder.Facing(held.FacesHolder.Value, back.InverseTransformDirection(Vector3.down))
                        : Quaternion.identity;
                    onBack = true;
                }
                else if (TrySheath(held.Bone, out var sheath))
                {
                    point = sheath;
                    turn = Quaternion.identity;
                }
            }

            // Its own size whatever the skeleton's scale, then turned (about its length too, so its holder's side faces
            // the way the place wants) and moved so that its grip lies on the point, or off the back by its depth.
            var look = held.Look.transform;
            look.SetParent(point, false);
            var boneScale = point.lossyScale.x;
            look.localScale = boneScale > 0f ? held.Size / boneScale : held.Size;
            var gripTurn = Quaternion.Inverse(look.rotation) * held.Grip.rotation;
            look.rotation = point.rotation * turn * Quaternion.Inverse(gripTurn);
            var standOff = onBack ? BackFinder.Strap + ExtentOf(held, Quaternion.Inverse(turn) * Vector3.back) * held.Grip.lossyScale.z : 0f;
            look.position += point.position + point.forward * standOff - held.Grip.position;
        }

        // A hand's spot across the back, made the first time it is asked for, from where the idle carries the back.
        bool TryBack(HumanBodyBones hand, out Transform back)
        {
            back = null;
            if (!IsHand(hand))
                return false;
            if (_backs.TryGetValue(hand, out back))
                return true;
            var chest = BackFinder.Chest(_humanoid);
            if (chest == null || !BackFinder.TryFind(_prefab, IdleClip, hand == HumanBodyBones.RightHand, out var pose))
                return false;

            back = new GameObject("Back " + hand).transform;
            back.SetParent(chest, false);
            back.gameObject.layer = Root.layer;
            back.SetPositionAndRotation(chest.position + chest.rotation * pose.position, chest.rotation * pose.rotation);
            _backs.Add(hand, back);
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
                UnityEngine.Object.Destroy(Root);
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
            // brings it round to the body in the hand.
            public Vector3? FacesHolder;
            public Quaternion InHandTurn;
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
                UnityEngine.Object.Destroy(collider);
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
