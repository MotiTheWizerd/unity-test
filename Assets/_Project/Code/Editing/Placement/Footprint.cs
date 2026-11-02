using System;
using System.Numerics;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// An object's ground footprint at scale 1 and no rotation: a rectangle <see cref="SizeX"/> by
    /// <see cref="SizeZ"/> meters centered on its pivot. Used to space objects so they don't overlap.
    /// </summary>
    public readonly struct Footprint
    {
        public Footprint(float sizeX, float sizeZ)
        {
            if (!(sizeX >= 0f) || !(sizeZ >= 0f) || float.IsInfinity(sizeX) || float.IsInfinity(sizeZ))
                throw new ArgumentOutOfRangeException(nameof(sizeX), "Footprint sizes must be finite and not negative.");
            SizeX = sizeX;
            SizeZ = sizeZ;
        }

        public float SizeX { get; }

        public float SizeZ { get; }

        /// <summary>
        /// Half the footprint's length along <paramref name="direction"/> (unit, on the ground plane, as (x, z))
        /// when the object is turned <paramref name="yawDegrees"/> and scaled by <paramref name="scale"/>.
        /// </summary>
        public float HalfLengthAlong(Vector2 direction, float yawDegrees, float scale)
        {
            var axisX = PlacementMath.LocalXAxis(yawDegrees);
            var axisZ = new Vector2(-axisX.Y, axisX.X); // local +Z is +X turned a quarter left
            var half = Math.Abs(Vector2.Dot(direction, axisX)) * SizeX + Math.Abs(Vector2.Dot(direction, axisZ)) * SizeZ;
            return half * 0.5f * scale;
        }
    }
}
