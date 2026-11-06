using Forge.Game.Player;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// One way of framing the player: first person today, third person (Skyrim's other half) next. A mode only says
    /// where the camera goes; the director puts the engine's camera there, so switching modes is swapping one object.
    /// </summary>
    public interface ICameraMode
    {
        /// <summary>How the player's body turns while this mode frames it (<see cref="PlayerCharacter.Facing"/>).</summary>
        BodyFacing Facing { get; }

        /// <summary>Whether the player's body shows (in first person the camera is inside it).</summary>
        bool ShowsBody { get; }

        /// <summary>The camera's pose this frame of <paramref name="deltaTime"/> seconds.</summary>
        ViewPose Frame(PlayerCharacter player, float deltaTime);
    }
}
