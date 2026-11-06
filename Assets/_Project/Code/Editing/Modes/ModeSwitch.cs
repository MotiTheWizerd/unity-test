using System;

namespace Forge.Editing.Modes
{
    /// <summary>
    /// Switches the app between editing and playing (<see cref="AppMode"/>). Playing freezes the edit session
    /// (<see cref="EditSession.Freeze"/>), so nothing that runs while playing can write the world, and so never a plugin;
    /// going back to editing thaws it. Playing waits while the agent's draft is open, since that is an edit in progress.
    /// </summary>
    public sealed class ModeSwitch
    {
        /// <summary>What the session's refusals say the world is frozen for.</summary>
        public const string PlayingReason = "playing";

        readonly EditSession _session;
        IDisposable _freeze;

        public ModeSwitch(EditSession session) => _session = session ?? throw new ArgumentNullException(nameof(session));

        public AppMode Mode => _freeze != null ? AppMode.Playing : AppMode.Editing;

        public bool IsPlaying => _freeze != null;

        /// <summary>Raised after the mode changes, with the new mode.</summary>
        public event Action<AppMode> Changed;

        /// <summary>
        /// Starts playing; true if playing now (also when already). Refused, with <paramref name="refusal"/> saying why
        /// for the user, while the agent's draft is open.
        /// </summary>
        public bool TryPlay(out string refusal)
        {
            refusal = null;
            if (IsPlaying)
                return true;
            if (_session.Draft != null)
            {
                refusal = "Play waits: the agent's build is open. Accept or discard it first.";
                return false;
            }

            _freeze = _session.Freeze(PlayingReason);
            Changed?.Invoke(AppMode.Playing);
            return true;
        }

        /// <summary>Goes back to editing (nothing happens when already editing); the world can be edited again.</summary>
        public void Edit()
        {
            if (!IsPlaying)
                return;

            _freeze.Dispose();
            _freeze = null;
            Changed?.Invoke(AppMode.Editing);
        }
    }
}
