using System;
using System.Numerics;

namespace Forge.Editing.State
{
    /// <summary>A camera pose as it is kept between sessions: a position in world coordinates and a rotation.</summary>
    public sealed class SavedPose
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public float RotationX { get; set; }
        public float RotationY { get; set; }
        public float RotationZ { get; set; }
        public float RotationW { get; set; } = 1f;

        public static SavedPose Of(Vector3 position, Quaternion rotation) => new SavedPose
        {
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            RotationX = rotation.X,
            RotationY = rotation.Y,
            RotationZ = rotation.Z,
            RotationW = rotation.W,
        };

        public Vector3 Position => new Vector3(X, Y, Z);

        /// <summary>The rotation, normalized; identity when the file held none that can be one.</summary>
        public Quaternion Rotation
        {
            get
            {
                var rotation = new Quaternion(RotationX, RotationY, RotationZ, RotationW);
                var length = rotation.Length();
                return length > 1e-6f && !float.IsNaN(length) && !float.IsInfinity(length) ? Quaternion.Normalize(rotation) : Quaternion.Identity;
            }
        }

        /// <summary>Whether every number is finite, so the pose can be used.</summary>
        public bool IsValid => Finite(X) && Finite(Y) && Finite(Z) && Finite(RotationX) && Finite(RotationY) && Finite(RotationZ) && Finite(RotationW);

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
