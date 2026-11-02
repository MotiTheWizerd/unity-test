using System.Numerics;

namespace Forge.Editing.Placement
{
    /// <summary>Where an object's pivot goes and how it is turned.</summary>
    public readonly struct ObjectPose
    {
        public ObjectPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        /// <summary>Where the pivot goes.</summary>
        public Vector3 Position { get; }

        public Quaternion Rotation { get; }
    }
}
