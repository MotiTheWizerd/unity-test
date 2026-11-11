using System.Numerics;

namespace Forge.Game.Motion
{
    /// <summary>
    /// An actor's body in the world, the player's or a character's: it moves as far as the world lets it (collisions,
    /// steps, slopes, other actors) and says what it touched. Positions are the feet, in the coordinates of whoever drives
    /// it (the scene's, for the engine's body). The rules (<see cref="Player.PlayerLocomotion"/>) decide how to move; a
    /// motor only carries the moves out, so another body (a swimmer, a horse, a test) can stand in.
    /// </summary>
    public interface IMotor
    {
        /// <summary>Where the feet are: the bottom of the body.</summary>
        Vector3 Position { get; }

        /// <summary>Moves by <paramref name="displacement"/>, stopped and slid along by what is in the way.</summary>
        MotorContact Move(Vector3 displacement);

        /// <summary>Puts the feet at <paramref name="feet"/> at once, through nothing.</summary>
        void Teleport(Vector3 feet);
    }
}
