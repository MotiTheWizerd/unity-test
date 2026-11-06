using System.Numerics;
using Forge.Core.Geometry;
using Forge.Game.Player;

namespace Forge.Game.Cameras
{
    /// <summary>Seeing through the player's eyes: the camera at <see cref="PlayerCharacter.Eyes"/>, looking along its look.</summary>
    public sealed class FirstPersonCamera : ICameraMode
    {
        public ViewPose Frame(PlayerCharacter player, float deltaTime) => new ViewPose(player.Eyes, Looking(player.Look));

        /// <summary>
        /// The rotation of a look: turned to its heading around +Y, then tilted by its pitch (a turn around +X points the
        /// view down, so looking up is the negative turn).
        /// </summary>
        public static Quaternion Looking(PlayerLook look) =>
            Quaternion.CreateFromYawPitchRoll(look.Heading * Compass.DegreesToRadians, -look.Pitch * Compass.DegreesToRadians, 0f);
    }
}
