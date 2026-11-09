using System.Numerics;

namespace Forge.Game.Player
{
    /// <summary>
    /// What the player is told to do this frame, whoever tells it: the keyboard and mouse today, a gamepad, a test, or a
    /// script later. Plain values, so the player's rules never read a device.
    /// </summary>
    public readonly struct PlayerCommands
    {
        public static readonly PlayerCommands None = default;

        /// <param name="move">Where to walk relative to the heading: x to the right, y forward, each in [-1, 1].</param>
        /// <param name="turn">Degrees to look this frame: x to the right, y up.</param>
        /// <param name="sprint">Held to run faster.</param>
        /// <param name="jump">Pressed this frame.</param>
        /// <param name="toggleWalk">Pressed this frame: switches between walking and the everyday run, as Skyrim's Caps Lock.</param>
        /// <param name="walk">Walk this frame whatever was switched (held, or forced: too tired to run).</param>
        /// <param name="activate">Pressed this frame: use what is in sight (take it, later open it, talk), Skyrim's Activate.</param>
        /// <param name="toggleCrouch">Pressed this frame: crouches, or stands back up, as Skyrim's sneak key.</param>
        public PlayerCommands(Vector2 move, Vector2 turn, bool sprint, bool jump, bool toggleWalk = false, bool walk = false, bool activate = false,
            bool toggleCrouch = false)
        {
            Move = move;
            Turn = turn;
            Sprint = sprint;
            Jump = jump;
            ToggleWalk = toggleWalk;
            Walk = walk;
            Activate = activate;
            ToggleCrouch = toggleCrouch;
        }

        public Vector2 Move { get; }

        public Vector2 Turn { get; }

        public bool Sprint { get; }

        public bool Jump { get; }

        public bool ToggleWalk { get; }

        public bool Walk { get; }

        public bool Activate { get; }

        public bool ToggleCrouch { get; }

        /// <summary>These commands as a rule lets them through (too tired to sprint, too laden to run): the rest unchanged.</summary>
        public PlayerCommands Allowing(bool sprint, bool jump, bool walk) =>
            new PlayerCommands(Move, Turn, sprint, jump, ToggleWalk, walk, Activate, ToggleCrouch);

        /// <summary>These commands with the crouch key let through or not (no room to stand, a body that can't crouch): the rest unchanged.</summary>
        public PlayerCommands AllowingCrouchToggle(bool toggle) =>
            new PlayerCommands(Move, Turn, Sprint, Jump, ToggleWalk, Walk, Activate, toggle);
    }
}
