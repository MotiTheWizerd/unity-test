using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Forge.Play
{
    /// <summary>
    /// A character's look going limp, Skyrim's way (a ragdoll): its animations stop (<see cref="CharacterModel.Animated"/>)
    /// and its body's parts (its <see cref="Hitboxes"/>, fitted to its own skin) become solid physical bodies, one each for
    /// the pelvis, the chest, the head, each upper arm, forearm, thigh and shin, the parts between carried along (the belly
    /// with the pelvis, the neck with the chest, a hand with its forearm, a foot with its shin), joined at their bones by
    /// joints that bend as a body does (<see cref="Parts"/>: knees and elbows one way, hips and shoulders about a cone, the
    /// back and the neck a little), measured from the T-pose its avatar was made in, however it lies when it goes limp; so it
    /// falls from where its animations left it and lies as the world has it. Its parts are clutter
    /// (<see cref="ClutterLayer"/>): actors walk over it and push it aside, the camera looks past it, anyone may take it up
    /// by a part and drag it (<see cref="Carriable"/>), and a blow or an arrow still meets it part by part. Stiffened
    /// (<see cref="Stiffen"/>), it is a look its animations pose again, its hitboxes as they were. Made on a look just
    /// built, before anything poses it (its bones that no avatar names kept as they stand); runtime-only, it goes with the
    /// look.
    /// </summary>
    public sealed class Ragdoll
    {
        /// <summary>A grown body's weight, kilograms, shared out over its parts; until a race's record gives its own.</summary>
        public const float Mass = 70f;

        // In the look's own frame at rest: x its right, y up, z ahead.
        static readonly Vector3 Right = Vector3.right, Up = Vector3.up, Ahead = Vector3.forward;

        // The parts with a body of their own, each its share of the weight (more or less a person's) and how it hangs from
        // the body above it, for the right side (the left mirrored); the pelvis hangs from nothing. Angles are turns about
        // the axis as the engine turns (a thigh swung ahead is -90° about the right), from the look at rest (a T-pose).
        static readonly (HumanBodyBones Bone, float Share, Hang? Hang)[] Parts =
        {
            (HumanBodyBones.Hips, 0.24f, null),
            // Bowing 40° ahead, arching 20° back, leaning and twisting a little.
            (HumanBodyBones.Chest, 0.25f, new Hang(Right, -20f, 40f, Ahead, 20f, 20f)),
            // Nodding 40° ahead, 35° back, tilting 25°, turning 45°.
            (HumanBodyBones.Head, 0.08f, new Hang(Right, -35f, 40f, Ahead, 25f, 45f)),
            // Down 80° to the side, up 60°; 60° ahead or behind, twisting 40°.
            (HumanBodyBones.RightUpperArm, 0.03f, new Hang(Ahead, -80f, 60f, Up, 60f, 40f)),
            (HumanBodyBones.LeftUpperArm, 0.03f, new Hang(Ahead, -80f, 60f, Up, 60f, 40f).Mirrored),
            // Bending ahead up to 140°, never back: a hinge.
            (HumanBodyBones.RightLowerArm, 0.025f, new Hang(Up, -140f, 0f, Ahead, 0f, 0f)),
            (HumanBodyBones.LeftLowerArm, 0.025f, new Hang(Up, -140f, 0f, Ahead, 0f, 0f).Mirrored),
            // Up to 90° ahead, 20° back; 30° out or in, twisting 20°.
            (HumanBodyBones.RightUpperLeg, 0.10f, new Hang(Right, -90f, 20f, Ahead, 30f, 20f)),
            (HumanBodyBones.LeftUpperLeg, 0.10f, new Hang(Right, -90f, 20f, Ahead, 30f, 20f).Mirrored),
            // Bending back up to 140°, never ahead: a hinge.
            (HumanBodyBones.RightLowerLeg, 0.06f, new Hang(Right, 0f, 140f, Ahead, 0f, 0f)),
            (HumanBodyBones.LeftLowerLeg, 0.06f, new Hang(Right, 0f, 140f, Ahead, 0f, 0f).Mirrored),
        };

        // How hard the solver works on its joints, so a part dragged by a hand doesn't stretch from the rest; how fast a
        // part sunk into the ground at the start comes out of it, meters a second (gently, not flung); how quickly it stops
        // turning (a limp body doesn't spin).
        const int SolverIterations = 12;
        const int SolverVelocityIterations = 4;
        const float MostDepenetration = 1f;
        const float AngularDamping = 0.5f;

        // How far a joint may come apart, meters, or turn past its limit, degrees, before it is set right at once.
        const float ProjectionDistance = 0.02f;
        const float ProjectionAngle = 5f;

        readonly CharacterModel _model;
        readonly IReadOnlyList<Hitbox> _hitboxes;
        readonly Transform[] _skeleton;
        readonly Vector3[] _restPositions;
        readonly Quaternion[] _restRotations;
        readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        readonly List<(Collider, Collider)> _ignored = new List<(Collider, Collider)>();

        /// <param name="model">A look just built, standing at rest.</param>
        /// <param name="hitboxes">Its hitboxes (<see cref="Hitboxes.Build"/>).</param>
        public Ragdoll(CharacterModel model, IReadOnlyList<Hitbox> hitboxes)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _hitboxes = hitboxes ?? throw new ArgumentNullException(nameof(hitboxes));
            // Below the look's own object, which faces as its body does: each bone at rest as its humanoid's avatar has it
            // (a T-pose, whatever pose the look stands in when made: arms down, for some), else as it stands now.
            var root = model.Root.transform;
            _skeleton = Array.FindAll(root.GetComponentsInChildren<Transform>(true), bone => bone != root);
            _restPositions = new Vector3[_skeleton.Length];
            _restRotations = new Quaternion[_skeleton.Length];
            var tPose = TPose(model);
            for (var i = 0; i < _skeleton.Length; i++)
            {
                var bone = _skeleton[i];
                var atRest = tPose.TryGetValue(bone.name, out var skeletonBone);
                _restPositions[i] = atRest ? skeletonBone.position : bone.localPosition;
                _restRotations[i] = atRest ? skeletonBone.rotation : bone.localRotation;
            }
        }

        // Each bone's place on its parent in the T-pose its humanoid's avatar was made in, by the bone's name; none without one.
        static Dictionary<string, SkeletonBone> TPose(CharacterModel model)
        {
            var bones = new Dictionary<string, SkeletonBone>();
            foreach (var animator in model.Animators)
            {
                if (!animator.isHuman || animator.avatar == null)
                    continue;
                foreach (var bone in animator.avatar.humanDescription.skeleton)
                    bones[bone.name] = bone;
                break;
            }

            return bones;
        }

        /// <summary>Whether it lies limp.</summary>
        public bool Limp { get; private set; }

        /// <summary>Where its pelvis is now (the scene's): where it lies, limp.</summary>
        public Vector3 Pelvis
        {
            get
            {
                var hips = _model.Bone(HumanBodyBones.Hips);
                return hips != null ? hips.position : _model.Root.transform.position;
            }
        }

        /// <summary>
        /// Goes limp where its animations left it (nothing for a look without a pelvis's hitbox): its joints are set as
        /// they would be at rest, then it falls from its pose as it is.
        /// </summary>
        public void GoLimp()
        {
            if (Limp)
                return;
            var hips = _model.Bone(HumanBodyBones.Hips);
            if (hips == null || !HasHitbox(hips))
                return;
            Limp = true;
            _model.Animated = false;

            // Joints measure their bends from the pose they are made in, so they are made at rest, then it is put back.
            var posed = Pose();
            Rest();
            foreach (var hitbox in _hitboxes)
                Hitboxes.Solid(hitbox, ClutterLayer.Index >= 0 ? ClutterLayer.Index : 0);
            var parts = MakeBodies();
            var look = _model.Root.transform;
            foreach (var (bone, hang) in parts)
            {
                if (hang.HasValue)
                    Join(bone, hang.Value, look);
            }

            IgnoreTouching();
            Pose(posed);
            foreach (var body in _bodies)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Stiffens again where it lies (got up): its bodies and joints go, its hitboxes ride their bones again, and its
        /// animations pose it.
        /// </summary>
        public void Stiffen()
        {
            if (!Limp)
                return;
            Limp = false;
            foreach (var (a, b) in _ignored)
            {
                if (a != null && b != null)
                    Physics.IgnoreCollision(a, b, false);
            }

            _ignored.Clear();
            foreach (var body in _bodies)
            {
                if (body == null)
                    continue;
                var part = body.gameObject;
                if (part.TryGetComponent<Carried>(out var carried))
                    Object.DestroyImmediate(carried);
                if (part.TryGetComponent<CharacterJoint>(out var joint))
                    Object.DestroyImmediate(joint);
                if (part.TryGetComponent<Carriable>(out var carriable))
                    Object.DestroyImmediate(carriable);
            }

            foreach (var body in _bodies)
            {
                if (body != null)
                    Object.DestroyImmediate(body);
            }

            _bodies.Clear();
            foreach (var hitbox in _hitboxes)
            {
                if (hitbox != null)
                    Hitboxes.Ride(hitbox);
            }

            _model.Animated = true;
        }

        // A body on each part's bone that has a hitbox, its share of the weight (shared over the parts there are), the
        // hitboxes on it and the bones between its parts; each body's bone and how it hangs.
        List<(Transform Bone, Hang? Hang)> MakeBodies()
        {
            var made = new List<(Transform Bone, Hang? Hang)>();
            var shares = 0f;
            foreach (var (humanBone, share, _) in Parts)
            {
                var bone = _model.Bone(humanBone);
                if (bone != null && HasHitbox(bone))
                    shares += share;
            }

            var frame = _model.Root.transform;
            foreach (var (humanBone, share, hang) in Parts)
            {
                var bone = _model.Bone(humanBone);
                if (bone == null || !HasHitbox(bone))
                    continue;
                var body = bone.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.mass = Mass * share / shares;
                body.angularDamping = AngularDamping;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.solverIterations = SolverIterations;
                body.solverVelocityIterations = SolverVelocityIterations;
                body.maxDepenetrationVelocity = MostDepenetration;
                var carriable = bone.gameObject.AddComponent<Carriable>();
                carriable.Frame = frame;
                carriable.Whole = _bodies;
                _bodies.Add(body);
                made.Add((bone, hang));
            }

            return made;
        }

        // A joint tying `bone`'s body to the nearest body above it, at the bone, bending as `hang` says about axes laid in
        // the look's frame `look` as it stands now, at rest.
        void Join(Transform bone, Hang hang, Transform look)
        {
            var above = bone.parent;
            while (above != null && !above.TryGetComponent<Rigidbody>(out _))
                above = above.parent;
            if (above == null)
                return;

            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = above.GetComponent<Rigidbody>();
            joint.anchor = Vector3.zero;
            joint.axis = bone.InverseTransformDirection(look.TransformDirection(hang.Axis));
            joint.swingAxis = bone.InverseTransformDirection(look.TransformDirection(hang.SwingAxis));
            // A joint's twist runs against the engine's turn about the same axis.
            joint.lowTwistLimit = new SoftJointLimit { limit = -hang.Most };
            joint.highTwistLimit = new SoftJointLimit { limit = -hang.Least };
            joint.swing1Limit = new SoftJointLimit { limit = hang.Swing };
            joint.swing2Limit = new SoftJointLimit { limit = hang.Swing2 };
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.projectionDistance = ProjectionDistance;
            joint.projectionAngle = ProjectionAngle;
        }

        // Its parts that already touch at rest (a thigh in the pelvis, the neck in the chest) don't knock into each other,
        // or it would push itself apart; parts apart at rest do (a hand on a thigh).
        void IgnoreTouching()
        {
            for (var i = 0; i < _hitboxes.Count; i++)
            {
                var a = _hitboxes[i].GetComponent<Collider>();
                for (var j = i + 1; j < _hitboxes.Count; j++)
                {
                    var b = _hitboxes[j].GetComponent<Collider>();
                    if (a.attachedRigidbody == b.attachedRigidbody ||
                        !Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation,
                            out _, out _))
                        continue;
                    Physics.IgnoreCollision(a, b, true);
                    _ignored.Add((a, b));
                }
            }
        }

        bool HasHitbox(Transform bone)
        {
            foreach (var hitbox in _hitboxes)
            {
                if (hitbox != null && hitbox.transform.parent == bone)
                    return true;
            }

            return false;
        }

        // Its skeleton's pose now, every bone's place on its parent.
        (Vector3, Quaternion)[] Pose()
        {
            var pose = new (Vector3, Quaternion)[_skeleton.Length];
            for (var i = 0; i < _skeleton.Length; i++)
            {
                if (_skeleton[i] != null)
                    pose[i] = (_skeleton[i].localPosition, _skeleton[i].localRotation);
            }

            return pose;
        }

        // Its skeleton posed as `pose` has it, for the physics too.
        void Pose((Vector3 Position, Quaternion Rotation)[] pose)
        {
            for (var i = 0; i < _skeleton.Length; i++)
            {
                if (_skeleton[i] != null)
                    _skeleton[i].SetLocalPositionAndRotation(pose[i].Position, pose[i].Rotation);
            }

            Physics.SyncTransforms();
        }

        // Its skeleton at rest, in its T-pose.
        void Rest()
        {
            for (var i = 0; i < _skeleton.Length; i++)
            {
                if (_skeleton[i] != null)
                    _skeleton[i].SetLocalPositionAndRotation(_restPositions[i], _restRotations[i]);
            }

            Physics.SyncTransforms();
        }

        // How a part hangs from the body above it: the axis it bends about, the least and the most it bends (degrees, as
        // the engine turns about the axis), the axis it swings about either way and how far, and how far it swings about
        // the third; axes in the look's frame at rest.
        readonly struct Hang
        {
            public Hang(Vector3 axis, float least, float most, Vector3 swingAxis, float swing, float swing2)
            {
                Axis = axis;
                Least = least;
                Most = most;
                SwingAxis = swingAxis;
                Swing = swing;
                Swing2 = swing2;
            }

            public Vector3 Axis { get; }
            public float Least { get; }
            public float Most { get; }
            public Vector3 SwingAxis { get; }
            public float Swing { get; }
            public float Swing2 { get; }

            // The left side's, from the right's: mirrored across the body's middle, a turn's axis is the mirror's, reversed
            // (a turn is a pseudo-vector), so the same bends mirror.
            public Hang Mirrored => new Hang(Mirror(Axis), Least, Most, Mirror(SwingAxis), Swing, Swing2);

            static Vector3 Mirror(Vector3 axis) => new Vector3(axis.x, -axis.y, -axis.z);
        }
    }
}
