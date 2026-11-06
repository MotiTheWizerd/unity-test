using System.Numerics;

namespace Forge.Game.Cameras
{
    /// <summary>What a camera can't pass through (the ground, walls, the placed objects), asked by the camera modes.</summary>
    public interface ICameraObstacles
    {
        /// <summary>
        /// How far a ball of <paramref name="radius"/> meters travels from <paramref name="from"/> toward
        /// <paramref name="to"/> before it touches something: the whole distance when nothing is in the way.
        /// </summary>
        float Clearance(Vector3 from, Vector3 to, float radius);
    }
}
