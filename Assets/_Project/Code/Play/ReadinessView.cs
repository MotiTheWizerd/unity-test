using System;
using Forge.Game.Equipment;

namespace Forge.Play
{
    /// <summary>
    /// An actor's ready-weapon shown on its look: the rules take their draw and sheathe times from the look's clips (its
    /// own, and its weapons' styles' by name: <see cref="CharacterModel.TryReadyTime"/>), and the moments in them when the
    /// hand takes the weapon and lets it go (<see cref="SheathFinder"/> at the hip, <see cref="BackFinder"/> on the back);
    /// each change plays on the look (the style's draw on the arms, the drawn moves underneath), and the weapons go from
    /// where they are carried to the hand and back at those moments. Bound while the look exists (the player while
    /// spawned); the rules never know it is there.
    /// </summary>
    public sealed class ReadinessView : IDisposable
    {
        readonly WeaponReadiness _readiness;
        readonly CharacterModel _model;

        public ReadinessView(WeaponReadiness readiness, CharacterModel model)
        {
            _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _readiness.SetTimes(model.DrawSeconds, model.SheatheSeconds, model.DrawnAt, model.SheathedAt);
            _readiness.Timings = new ModelTimings(model);
            _readiness.Changed += OnChanged;
            _readiness.InHandChanged += OnInHandChanged;
            _model.Animate(_readiness.State);
            _model.InHand = _readiness.InHand;
        }

        public void Dispose()
        {
            _readiness.Changed -= OnChanged;
            _readiness.InHandChanged -= OnInHandChanged;
            _readiness.Timings = null;
        }

        void OnChanged(Readiness state) =>
            _model.Animate(state, state == Readiness.Drawing ? _readiness.DrawAnimation
                : state == Readiness.Sheathing ? _readiness.SheatheAnimation : null);

        void OnInHandChanged(bool inHand) => _model.InHand = inHand;

        sealed class ModelTimings : IReadyTimings
        {
            readonly CharacterModel _model;

            public ModelTimings(CharacterModel model) => _model = model;

            public bool TryGet(string animation, out ReadyTiming timing) => _model.TryReadyTime(animation, out timing);
        }
    }
}
