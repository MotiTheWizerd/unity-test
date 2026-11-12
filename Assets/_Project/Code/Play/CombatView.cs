using System;
using Forge.Data.Records;
using Forge.Game.Combat;

namespace Forge.Play
{
    /// <summary>
    /// An actor's fighting shown on its look: the rules time each move as the look's clip plays it
    /// (<see cref="CharacterModel.TryTime"/>, a release by <see cref="CharacterModel.TryTimeRelease"/>), and each swing
    /// plays its move on the look's arms; a held one (a bow's draw) is held still at the end of its window while the rules
    /// hold it, and once let go goes into its move's release (a bow's: the string let go, the bow coming down), else plays
    /// on from there; a raised block that takes a blow is jolted by its style's reaction, then held up again. A blow's
    /// impact freezes the look as long as it stops the fighting (hit-stop), and a blade that bounced off plays its move
    /// back to where it came from. A blow that reaches the body plays its hit over whatever the look does
    /// (<see cref="ActorCombat.HitAnimation"/>, else the look's own <see cref="HumanoidAnimation.HitMove"/>; one of its
    /// variants by chance). Bound while the look exists (the player while spawned); the rules never know it is there.
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
            _combat.Blocked += OnBlocked;
            _combat.Stalled += _model.Freeze;
            _combat.Recoiled += OnRecoiled;
            _combat.Struck += OnStruck;
        }

        public void Dispose()
        {
            _combat.SwingStarted -= OnSwingStarted;
            _combat.SwingHeld -= OnSwingHeld;
            _combat.SwingLetGo -= OnSwingLetGo;
            _combat.SwingEnded -= OnSwingEnded;
            _combat.Blocked -= OnBlocked;
            _combat.Stalled -= _model.Freeze;
            _combat.Recoiled -= OnRecoiled;
            _combat.Struck -= OnStruck;
            _combat.Timings = null;
        }

        // A blow reached the body: it gives with it, over whatever it does.
        void OnStruck(Blow blow) => _model.Play(_model.Vary(_combat.HitAnimation ?? HumanoidAnimation.HitMove));

        // A blade that met what it can't cut through plays back the way it came, as long as the rules let it come back.
        void OnRecoiled(Swing swing) => _model.Rebound(swing.Move.Animation, ActorCombat.RecoilSeconds);

        // A raised shield took a blow: it jolts, then is held up again.
        void OnBlocked(Swing guard, CombatMove reaction)
        {
            if (reaction != null)
                _model.React(guard.Move.Animation, reaction.Animation);
        }

        void OnSwingStarted(Swing swing) => _model.Play(swing.Move.Animation);

        void OnSwingHeld(Swing swing) => _model.Hold(swing.Move.Animation, swing.Timing.HitTo);

        void OnSwingLetGo(Swing swing)
        {
            if (swing.Move.Release == null || !_model.Release(swing.Move.Animation, swing.Move.Release))
                _model.Resume(swing.Move.Animation, swing.Timing.HitTo);
        }

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

            public bool TryGet(string animation, MoveMeasure measure, out MoveTiming timing) => _model.TryTime(animation, measure, out timing);

            public bool TryGetRelease(string animation, out float seconds) => _model.TryTimeRelease(animation, out seconds);
        }
    }
}
