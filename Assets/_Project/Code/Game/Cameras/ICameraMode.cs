using Forge.Game.Player;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// One way of framing the player: first person today, third person (Skyrim's other half) next. A mode only says
    /// where the camera goes; the director puts the engine's camera there, so switching modes is swapping one object.
    /// </summary>
    public interface ICameraMode
    {
        /// <summary>The camera's pose this frame of <paramref name="deltaTime"/> seconds.</summary>
        ViewPose Frame(PlayerCharacter player, float deltaTime);
    }
}
