using System;
using System.Numerics;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// Headings on the ground plane, in the engine's convention: yaw turns around +Y, and yaw 0
    /// leaves an object's local +X pointing along world +X. Ground vectors are (x, z).
    /// </summary>
    public static class PlacementMath
    {
        const float DegreesToRadians = (float)(Math.PI / 180.0);

        /// <summary>Where an object's local +X points on the ground when turned <paramref name="yawDegrees"/>.</summary>
        public static Vector2 LocalXAxis(float yawDegrees)
        {
            var radians = yawDegrees * DegreesToRadians;
            return new Vector2((float)Math.Cos(radians), -(float)Math.Sin(radians));
        }

        /// <summary>The yaw in [0, 360) that points an object's local +X (a fence's length) along <paramref name="direction"/>.</summary>
        public static float YawAlong(Vector2 direction) =>
            NormalizeDegrees((float)(Math.Atan2(-direction.Y, direction.X) / DegreesToRadians));

        public static float NormalizeDegrees(float degrees)
        {
            var wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }
    }
}
