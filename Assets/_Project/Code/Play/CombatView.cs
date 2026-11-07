using System;
using Forge.Game.Combat;

namespace Forge.Play
{
    /// <summary>
    /// An actor's fighting shown on its look: the rules time each move as the look's clip plays it
    /// (<see cref="CharacterModel.TryTime"/>), and each swing plays its move on the look's arms. Bound while the look
    /// exists (the player while spawned); the rules never know it is there.
    /// </summary>
    public sealed class CombatView : IDisposable
    {
        readonly ActorCombat _combat;
        readonly CharacterModel _model;

        public CombatView(ActorCombat combat, CharacterModel model)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _combat.Timings = new ModelTimings(model);
            _combat.SwingStarted += OnSwingStarted;
        }

        public void Dispose()
        {
            _combat.SwingStarted -= OnSwingStarted;
            _combat.Timings = null;
        }

        void OnSwingStarted(Swing swing) => _model.Play(swing.Move.Animation);

        sealed class ModelTimings : IMoveTimings
        {
            readonly CharacterModel _model;

            public ModelTimings(CharacterModel model) => _model = model;

            public bool TryGet(string animation, out MoveTiming timing) => _model.TryTime(animation, out timing);
        }
    }
}
