using System;
using Forge.Game.Equipment;

namespace Forge.Play
{
    /// <summary>
    /// An actor's ready-weapon shown on its look: the rules take their draw and sheathe times from the look's clips, and
    /// each change plays on it (a draw on the arms, the drawn moves underneath). Bound while the look exists (the player
    /// while spawned); the rules never know it is there.
    /// </summary>
    public sealed class ReadinessView : IDisposable
    {
        readonly WeaponReadiness _readiness;
        readonly CharacterModel _model;

        public ReadinessView(WeaponReadiness readiness, CharacterModel model)
        {
            _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _readiness.SetTimes(model.DrawSeconds, model.SheatheSeconds);
            _readiness.Changed += _model.Animate;
            _model.Animate(_readiness.State);
        }

        public void Dispose() => _readiness.Changed -= _model.Animate;
    }
}
