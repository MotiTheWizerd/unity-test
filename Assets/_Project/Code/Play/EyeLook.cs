using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A character's eyes turned to what it looks at: on each frame's pose (<see cref="CharacterModel.Posed"/>), after its
    /// head has turned (<see cref="HeadLook"/>, bound first), each eye turns about its own middle toward the point, as far
    /// as an eye turns in its lids (<see cref="MostTurn"/> aside, <see cref="MostTilt"/> up or down, from where the face
    /// looks; beyond, as far round as it can), quicker than the head (<see cref="TurnSpeed"/>), so they land on it first
    /// and the head comes round after; with nothing to look at they go back to looking ahead (<see cref="Ease"/>). Each
    /// eye aims from where it is, so a near point draws them together. The eyes are bones of their own: those the worn
    /// head brought (<see cref="CharacterHeads.WornBones"/>, found again when another is put on), else its humanoid's;
    /// a character without them looks with its head alone. Bound while the look exists.
    /// </summary>
    public sealed class EyeLook : IDisposable
    {
        /// <summary>Degrees an eye turns aside from where the face looks, at most.</summary>
        public const float MostTurn = 30f;

        /// <summary>Degrees an eye turns up or down, at most.</summary>
        public const float MostTilt = 20f;

        /// <summary>Degrees a second an eye turns as what it looks at moves, or another takes it.</summary>
        public const float TurnSpeed = 600f;

        /// <summary>Seconds the eyes take to settle on something, and to let go of it.</summary>
        public const float Ease = 0.12f;

        const string LeftName = "LeftEye";
        const string RightName = "RightEye";

        readonly CharacterModel _model;
        readonly Transform _face;
        readonly Func<Vector3?> _target;
        readonly CharacterHeads _heads;
        readonly List<Eye> _eyes = new List<Eye>(2);
        int _headWorn = -2;
        float _weight;

        /// <param name="eyeHeight">Meters from the feet to the eyes as it stands at rest (<see cref="CharacterModel.Eyes"/>).</param>
        /// <param name="target">The point it looks at now (the scene's); null: nothing.</param>
        public EyeLook(CharacterModel model, float eyeHeight, Func<Vector3?> target)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _face = model.Eyes(eyeHeight);
            _heads = model.Root.GetComponentInChildren<CharacterHeads>(true);
            _model.Posed += OnPosed;
        }

        /// <summary>The eyes it turns: none without eye bones or a face.</summary>
        public int EyeCount
        {
            get
            {
                FindEyes();
                return _eyes.Count;
            }
        }

        public void Dispose() => _model.Posed -= OnPosed;

        void OnPosed() => Look(Time.deltaTime);

        /// <summary>Turns the eyes <paramref name="deltaTime"/> seconds' worth toward the target, on the pose as it is now (each frame's, after the head's).</summary>
        public void Look(float deltaTime)
        {
            FindEyes();
            if (_eyes.Count == 0)
                return;

            var target = _target();
            _weight = Mathf.MoveTowards(_weight, target.HasValue ? 1f : 0f, deltaTime / Ease);
            var step = TurnSpeed * deltaTime;
            var shown = Mathf.SmoothStep(0f, 1f, _weight);
            foreach (var eye in _eyes)
            {
                // From its rest each frame: nothing else poses it, so last frame's turn would still be on it.
                eye.Bone.localRotation = eye.Rest;
                var looks = eye.Bone.rotation * eye.Looks;
                if (target.HasValue)
                {
                    Reach(Quaternion.Inverse(looks) * (target.Value - eye.Bone.position), out var aside, out var up);
                    eye.Aside = Mathf.MoveTowards(eye.Aside, aside, step);
                    eye.Up = Mathf.MoveTowards(eye.Up, up, step);
                }

                if (!(shown > 0f))
                    continue;
                var turn = Quaternion.Euler(-eye.Up * shown, eye.Aside * shown, 0f);
                eye.Bone.rotation = looks * turn * Quaternion.Inverse(eye.Looks);
            }
        }

        // The eyes of what it wears now, each as it is at rest: a head put on brings its own.
        void FindEyes()
        {
            var worn = _heads != null ? _heads.Worn : -1;
            if (worn == _headWorn && _eyes.TrueForAll(e => e.Bone != null))
                return;
            _headWorn = worn;
            _eyes.Clear();
            if (_face == null)
                return;

            if (_heads != null)
            {
                foreach (var bone in _heads.WornBones)
                {
                    if (bone != null && (bone.name.EndsWith(LeftName, StringComparison.Ordinal) ||
                                         bone.name.EndsWith(RightName, StringComparison.Ordinal)))
                        _eyes.Add(new Eye(bone, _face));
                }
            }

            if (_eyes.Count > 0)
                return;
            foreach (var which in new[] { HumanBodyBones.LeftEye, HumanBodyBones.RightEye })
            {
                var bone = _model.Bone(which);
                if (bone != null)
                    _eyes.Add(new Eye(bone, _face));
            }
        }

        // How far aside and up (degrees) toward `way` (the frame the eye looks ahead in) an eye can look.
        static void Reach(Vector3 way, out float aside, out float up)
        {
            aside = up = 0f;
            if (way.sqrMagnitude < 1e-8f)
                return;
            aside = Mathf.Clamp(Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg, -MostTurn, MostTurn);
            up = Mathf.Clamp(Mathf.Atan2(way.y, new Vector2(way.x, way.z).magnitude) * Mathf.Rad2Deg, -MostTilt, MostTilt);
        }

        sealed class Eye
        {
            public Eye(Transform bone, Transform face)
            {
                Bone = bone;
                Rest = bone.localRotation;
                // Where the face looks, in the eye's own frame at rest: both ride the head, so it holds however it turns.
                Looks = Quaternion.Inverse(bone.rotation) * face.rotation;
            }

            public Transform Bone { get; }

            /// <summary>Its turn on its parent as it was made.</summary>
            public Quaternion Rest { get; }

            /// <summary>The frame it looks ahead in (forward, the face's up), in its own at rest.</summary>
            public Quaternion Looks { get; }

            /// <summary>Where it is turned, degrees from ahead.</summary>
            public float Aside { get; set; }

            public float Up { get; set; }
        }
    }
}
