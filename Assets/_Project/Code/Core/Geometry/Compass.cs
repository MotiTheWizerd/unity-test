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

        /// <summary><paramref name="ground"/> (x, z) as something facing <paramref name="headingDegrees"/> has it: x to its right, y ahead.</summary>
        public static Vector2 Relative(Vector2 ground, float headingDegrees) =>
            new Vector2(Vector2.Dot(ground, Right(headingDegrees)), Vector2.Dot(ground, Forward(headingDegrees)));

        /// <summary>A way as something facing <paramref name="headingDegrees"/> has it (x to its right, y ahead), on the ground (x, z): <see cref="Relative"/> undone.</summary>
        public static Vector2 FromRelative(Vector2 relative, float headingDegrees) =>
            Right(headingDegrees) * relative.X + Forward(headingDegrees) * relative.Y;

        /// <summary>The heading that faces along <paramref name="ground"/> (x, z); north for none.</summary>
        public static float HeadingOf(Vector2 ground) =>
            ground.LengthSquared() < 1e-12f ? 0f : Normalize((float)(Math.Atan2(ground.X, ground.Y) / DegreesToRadians));

        /// <summary><paramref name="degrees"/> wrapped into [0, 360).</summary>
        public static float Normalize(float degrees)
        {
            var wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }
    }
}
