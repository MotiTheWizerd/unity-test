using System;
using System.Numerics;
using Forge.Core.Geometry;

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
        public static Vector2 LocalXAxis(float yawDegrees) => Compass.Right(yawDegrees);

        /// <summary>The yaw in [0, 360) that points an object's local +X (a fence's length) along <paramref name="direction"/>.</summary>
        public static float YawAlong(Vector2 direction) =>
            NormalizeDegrees((float)(Math.Atan2(-direction.Y, direction.X) / DegreesToRadians));

        /// <summary>The heading of a (possibly tilted) rotation: where its local +X points, seen from above.</summary>
        public static float HeadingOf(Quaternion rotation)
        {
            var axisX = Vector3.Transform(Vector3.UnitX, rotation);
            var flat = new Vector2(axisX.X, axisX.Z);
            return flat.LengthSquared() < 1e-12f ? 0f : YawAlong(Vector2.Normalize(flat));
        }

        /// <summary>
        /// <paramref name="rotation"/> turned around world +Y until its heading (<see cref="HeadingOf"/>) is
        /// <paramref name="headingDegrees"/>, keeping any tilt it has (an object aligned to a slope stays aligned).
        /// </summary>
        public static Quaternion WithHeading(Quaternion rotation, float headingDegrees)
        {
            var turn = (headingDegrees - HeadingOf(rotation)) * DegreesToRadians;
            return Quaternion.Normalize(Quaternion.Concatenate(rotation, Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn)));
        }

        public static float NormalizeDegrees(float degrees) => Compass.Normalize(degrees);
    }
}
