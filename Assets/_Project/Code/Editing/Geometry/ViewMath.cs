using System;

namespace Forge.Editing.Geometry
{
    /// <summary>Engine-free camera math for editor views.</summary>
    public static class ViewMath
    {
        /// <summary>
        /// Distance from a sphere's center at which a perspective camera sees the whole
        /// sphere, limited by whichever field of view (vertical or horizontal) is narrower.
        /// </summary>
        /// <param name="radius">Sphere radius, in meters.</param>
        /// <param name="verticalFovDegrees">Camera's vertical field of view, in degrees.</param>
        /// <param name="aspect">Width / height of the view.</param>
        public static float FitDistance(float radius, float verticalFovDegrees, float aspect)
        {
            if (radius < 0f || float.IsNaN(radius))
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must not be negative.");
            if (!(verticalFovDegrees > 0f && verticalFovDegrees < 180f))
                throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees), verticalFovDegrees, "Field of view must be in (0, 180) degrees.");
            if (!(aspect > 0f))
                throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "Aspect must be positive.");

            var halfVertical = verticalFovDegrees * Math.PI / 360.0;
            var halfHorizontal = Math.Atan(Math.Tan(halfVertical) * aspect);
            var halfAngle = Math.Min(halfVertical, halfHorizontal);
            return (float)(radius / Math.Sin(halfAngle));
        }
    }
}
