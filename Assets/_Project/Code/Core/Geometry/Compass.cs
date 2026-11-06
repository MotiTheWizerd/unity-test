using System;
using System.Numerics;

namespace Forge.Core.Geometry
{
    /// <summary>
    /// Headings as compass bearings, in degrees: 0 north (+Z), 90 east (+X). That is the engine's yaw around +Y, so a
    /// heading turns an object, a player or a camera alike. Ground vectors are (x, z).
    /// </summary>
    public static class Compass
    {
        public const float DegreesToRadians = (float)(Math.PI / 180.0);

        /// <summary>Which way a heading faces on the ground: north (0, 1) at 0, east (1, 0) at 90.</summary>
        public static Vector2 Forward(float headingDegrees)
        {
            var radians = headingDegrees * DegreesToRadians;
            return new Vector2((float)Math.Sin(radians), (float)Math.Cos(radians));
        }

        /// <summary>Right of a heading on the ground (where something turned that way has its local +X): east at 0.</summary>
        public static Vector2 Right(float headingDegrees)
        {
            var radians = headingDegrees * DegreesToRadians;
            return new Vector2((float)Math.Cos(radians), -(float)Math.Sin(radians));
        }

        /// <summary><paramref name="degrees"/> wrapped into [0, 360).</summary>
        public static float Normalize(float degrees)
        {
            var wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }
    }
}
