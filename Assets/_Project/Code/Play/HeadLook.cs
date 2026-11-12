using System;
using UnityEngine;

namespace Forge.Play
{
    /// <summary>
    /// A character's head turned to whom it looks at (<see cref="PlayedActor.LooksAt"/>): on each frame's pose
    /// (<see cref="CharacterModel.Posed"/>) the neck and the head turn its face (<see cref="CharacterModel.Eyes"/>, which
    /// look where the face does) toward their eyes, as far as a neck turns (<see cref="MostTurn"/> aside,
    /// <see cref="MostTilt"/> up or down, from where its body faces; beyond, it looks as far round as it can), at a
    /// head's pace (<see cref="TurnSpeed"/>), easing in as it starts looking and back to where its animation has it as it
    /// stops (<see cref="Ease"/>). Only the neck and the head turn (<see cref="CharacterModel.TurnHead"/>), so what the
    /// arms hold is undisturbed; it turns last, after the upper body is set. Bound while the look exists.
    /// </summary>
    public sealed class HeadLook : IDisposable
    {
        /// <summary>Degrees the face turns aside from where the body faces, at most.</summary>
        public const float MostTurn = 75f;

        /// <summary>Degrees the face tilts up or down, at most.</summary>
        public const float MostTilt = 35f;

        /// <summary>Degrees a second the face turns as whom it looks at moves, or another takes its eye.</summary>
        public const float TurnSpeed = 270f;

        /// <summary>Seconds the head takes to come round to someone, and to let go of them.</summary>
        public const float Ease = 0.3f;

        readonly CharacterModel _model;
        readonly Transform _eyes;
        readonly Func<Vector3?> _target;
        // Where the face is turned, in the body's own frame (its model's turn), and how much of the turn shows.
        Vector3 _way = Vector3.forward;
        float _weight;

        /// <param name="eyeHeight">Meters from the feet to the eyes as it stands at rest (<see cref="CharacterModel.Eyes"/>).</param>
        /// <param name="target">Where the eyes of whom it looks at are now (the scene's); null: no one.</param>
        public HeadLook(CharacterModel model, float eyeHeight, Func<Vector3?> target)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _eyes = model.Eyes(eyeHeight);
            _model.Posed += OnPosed;
        }

        public void Dispose() => _model.Posed -= OnPosed;

        void OnPosed()
        {
            // A look without a head has nothing to turn.
            if (_eyes == null)
                return;
            var body = _model.Root.transform;
            var target = _target();
            // Let go altogether, it starts again from wherever the animation has the face.
            if (!(_weight > 0f))
                _way = body.InverseTransformDirection(_eyes.forward);
            _weight = Mathf.MoveTowards(_weight, target.HasValue ? 1f : 0f, Time.deltaTime / Ease);
            if (target.HasValue)
                _way = Vector3.RotateTowards(_way, Reach(body.InverseTransformDirection(target.Value - _eyes.position)),
                    TurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            if (!(_weight > 0f))
                return;

            var turn = Quaternion.FromToRotation(_eyes.forward, body.TransformDirection(_way));
            _model.TurnHead(Quaternion.Slerp(Quaternion.identity, turn, Mathf.SmoothStep(0f, 1f, _weight)));
        }

        // The way toward `way` (the body's frame) a face can look: as far aside and up or down as a neck turns.
        static Vector3 Reach(Vector3 way)
        {
            if (way.sqrMagnitude < 1e-8f)
                return Vector3.forward;
            var aside = Mathf.Clamp(Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg, -MostTurn, MostTurn);
            var up = Mathf.Clamp(Mathf.Atan2(way.y, new Vector2(way.x, way.z).magnitude) * Mathf.Rad2Deg, -MostTilt, MostTilt);
            return Quaternion.Euler(-up, aside, 0f) * Vector3.forward;
        }
    }
}
