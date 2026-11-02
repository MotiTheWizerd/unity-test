using System.Numerics;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>A position on the ground, as command arguments give it.</summary>
    public sealed class GroundPoint
    {
        [Param("East-west position, in meters (x grows to the east).")]
        public float X { get; set; }

        [Param("North-south position, in meters (z grows to the north).")]
        public float Z { get; set; }

        /// <summary>The point as a ground vector (x, z).</summary>
        public Vector2 ToVector() => new Vector2(X, Z);
    }
}
