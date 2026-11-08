using System;
using Forge.Game.Combat;

namespace Forge.Play
{
    /// <summary>
    /// An actor's fighting shown on its look: the rules time each move as the look's clip plays it
    /// (<see cref="CharacterModel.TryTime"/>), and each swing plays its move on the look's arms; a held one (a bow's draw)
    /// is held still at the end of its window while the rules hold it, and plays on from there once let go. Bound while
    /// the look exists (the player while spawned); the rules never know it is there.
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
            _combat.SwingHeld += OnSwingHeld;
            _combat.SwingLetGo += OnSwingLetGo;
            _combat.SwingEnded += OnSwingEnded;
        }

        public void Dispose()
        {
            _combat.SwingStarted -= OnSwingStarted;
            _combat.SwingHeld -= OnSwingHeld;
            _combat.SwingLetGo -= OnSwingLetGo;
            _combat.SwingEnded -= OnSwingEnded;
            _combat.Timings = null;
        }

        void OnSwingStarted(Swing swing) => _model.Play(swing.Move.Animation);

        void OnSwingHeld(Swing swing) => _model.Hold(swing.Move.Animation, swing.Timing.HitTo);

        void OnSwingLetGo(Swing swing) => _model.Resume(swing.Move.Animation, swing.Timing.HitTo);

        // Stopped while held (the weapon put away): it fades out where it is.
        void OnSwingEnded(Swing swing)
        {
            if (swing.Holding)
                _model.Resume(swing.Move.Animation, 1f);
        }

        sealed class ModelTimings : IMoveTimings
        {
            readonly CharacterModel _model;

            public ModelTimings(CharacterModel model) => _model = model;

            public bool TryGet(string animation, bool shoots, out MoveTiming timing) => _model.TryTime(animation, shoots, out timing);
        }
    }
}
