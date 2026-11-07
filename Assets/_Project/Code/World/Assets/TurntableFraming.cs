using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Where a perspective camera stands to show a thing turning on a turntable whole, whichever way it faces, from a
    /// little above (<see cref="Pitch"/>). Turning, the thing sweeps a cylinder: its reach around the axis (the radius,
    /// from the drawn bounds' width and depth) and its half height. The cylinder must fit the view's width, and seen
    /// from above, its height and the near and far edges of its reach must fit the view's height; the nearer of the two
    /// distances that do so wins, so a long flat sword fills the view as well as a tall bottle does. Zoom brings the
    /// camera nearer (above 1) or further (below 1). Pure math, so the framing is testable without rendering.
    /// </summary>
    public static class TurntableFraming
    {
        /// <summary>Degrees the camera looks down on the turntable: enough to see the face of what lies flat (a sword, coins).</summary>
        public const float Pitch = 30f;

        /// <summary>Room around the thing, as a fraction of what it needs.</summary>
        public const float Margin = 1.1f;

        public const float MinZoom = 0.6f;
        public const float MaxZoom = 3f;

        /// <summary>
        /// The camera's distance from the centre of a thing reaching <paramref name="reach"/> m around its axis and
        /// <paramref name="halfHeight"/> m up and down from its centre, in a view of <paramref name="verticalFov"/> degrees
        /// and <paramref name="aspect"/> (width over height), closer by <paramref name="zoom"/>.
        /// </summary>
        public static float Distance(float reach, float halfHeight, float verticalFov, float aspect, float zoom)
        {
            reach = Mathf.Max(reach, 0.01f);
            halfHeight = Mathf.Max(halfHeight, 0.01f);
            var halfVertical = verticalFov * 0.5f * Mathf.Deg2Rad;
            var halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * Mathf.Max(aspect, 0.01f));
            var pitch = Pitch * Mathf.Deg2Rad;

            // Across, the reach is all that shows; up and down, the height foreshortened and the reach tilted into view.
            var across = reach / Mathf.Sin(halfHorizontal);
            var upDown = (halfHeight * Mathf.Cos(pitch) + reach * Mathf.Sin(pitch)) / Mathf.Sin(halfVertical);
            return Mathf.Max(across, upDown) * Margin / Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }

        /// <summary>The camera's pose <paramref name="distance"/> m from <paramref name="centre"/>, looking at it from the front (−Z), a little above.</summary>
        public static Pose CameraPose(Vector3 centre, float distance)
        {
            var rotation = Quaternion.Euler(Pitch, 0f, 0f);
            return new Pose(centre - rotation * Vector3.forward * distance, rotation);
        }
    }
}
