using System;
using Forge.Game.Equipment;

namespace Forge.Play
{
    /// <summary>
    /// An actor's ready-weapon shown on its look: the rules take their draw and sheathe times from the look's clips, and
    /// the moments in them when the hand takes the hilt and lets it go (<see cref="SheathFinder"/>); each change plays on
    /// the look (a draw on the arms, the drawn moves underneath), and the weapons go from sheath to hand and back at those
    /// moments. Bound while the look exists (the player while spawned); the rules never know it is there.
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
            _readiness.Changed += _model.Animate;
            _readiness.InHandChanged += OnInHandChanged;
            _model.Animate(_readiness.State);
            _model.InHand = _readiness.InHand;
        }

        public void Dispose()
        {
            _readiness.Changed -= _model.Animate;
            _readiness.InHandChanged -= OnInHandChanged;
        }

        void OnInHandChanged(bool inHand) => _model.InHand = inHand;
    }
}
