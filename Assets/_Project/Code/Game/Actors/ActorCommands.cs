using Forge.Game.Player;

namespace Forge.Game.Actors
{
    /// <summary>
    /// What an actor is told to do this frame, whoever minds it: the player's controls, a character's mind, a test. How
    /// to move (<see cref="PlayerCommands"/>, the rules' own input), the ready-weapon key, and each hand used (its attack
    /// pressed: a swing, or a shield raised) or let go of (released: a drawn bow loosed, a block lowered). Plain values,
    /// so what carries them out never knows who decided.
    /// </summary>
    public readonly struct ActorCommands
    {
        /// <summary>Nothing: standing still, the hands idle.</summary>
        public static readonly ActorCommands None = default;

        /// <param name="move">How to move and turn this frame.</param>
        /// <param name="readyWeapon">Pressed this frame: draws the weapons, or puts them away.</param>
        /// <param name="useRight">Pressed this frame: uses what the right hand holds as its style says (attacks, or raises a shield).</param>
        /// <param name="useLeft">Pressed this frame: the left hand likewise.</param>
        /// <param name="releaseRight">Released this frame: lets go of what the right hand held (looses a drawn bow, lowers a block).</param>
        /// <param name="releaseLeft">Released this frame: the left hand likewise.</param>
        public ActorCommands(PlayerCommands move, bool readyWeapon = false, bool useRight = false, bool useLeft = false,
            bool releaseRight = false, bool releaseLeft = false)
        {
            Move = move;
            ReadyWeapon = readyWeapon;
            UseRight = useRight;
            UseLeft = useLeft;
            ReleaseRight = releaseRight;
            ReleaseLeft = releaseLeft;
        }

        /// <summary>Only moving: the weapons and the hands left as they are.</summary>
        public static ActorCommands Moving(PlayerCommands move) => new ActorCommands(move);

        public PlayerCommands Move { get; }

        public bool ReadyWeapon { get; }

        public bool UseRight { get; }

        public bool UseLeft { get; }

        public bool ReleaseRight { get; }

        public bool ReleaseLeft { get; }

        /// <summary>Whether the right (<paramref name="right"/>) or the left hand is used this frame.</summary>
        public bool Uses(bool right) => right ? UseRight : UseLeft;

        /// <summary>Whether the right (<paramref name="right"/>) or the left hand lets go this frame.</summary>
        public bool Releases(bool right) => right ? ReleaseRight : ReleaseLeft;
    }
}
