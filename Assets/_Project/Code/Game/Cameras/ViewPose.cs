using System.Numerics;

namespace Forge.Game.Cameras
{
    /// <summary>Where a camera stands and which way it looks, in the coordinates of the player it frames.</summary>
    public readonly struct ViewPose
    {
        public ViewPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        /// <summary>The way it looks (its local +Z).</summary>
        public Vector3 Forward => Vector3.Transform(Vector3.UnitZ, Rotation);
    }
}
