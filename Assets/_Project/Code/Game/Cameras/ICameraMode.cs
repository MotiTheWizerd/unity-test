using Forge.Game.Player;

namespace Forge.Game.Cameras
{
    /// <summary>
    /// One way of framing the player: first person or third (Skyrim's two). A mode only says where the camera goes and
    /// how near it sees; the director puts the engine's camera there, so switching modes is swapping one object.
    /// </summary>
    public interface ICameraMode
    {
        /// <summary>How the player's body turns while this mode frames it (<see cref="PlayerCharacter.Facing"/>).</summary>
        BodyFacing Facing { get; }

        /// <summary>Whether the camera is inside the player's head (first person): all of the body shows but its head, which only casts its shadow.</summary>
        bool InsideHead { get; }

        /// <summary>How near the camera sees, meters; null: as near as the camera is set to.</summary>
        float? NearClip { get; }

        /// <summary>The camera's pose this frame of <paramref name="deltaTime"/> seconds.</summary>
        ViewPose Frame(PlayerCharacter player, float deltaTime);
    }
}
