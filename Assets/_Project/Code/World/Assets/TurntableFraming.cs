using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Where a perspective camera stands to show a thing turning on a turntable whole, whichever way it faces, from a
    /// little above (<see cref="Pitch"/>). Turning, the thing sweeps a cylinder: its reach around the axis (the radius)
    /// and its half height, from its drawn bounds as it leans (<see cref="Swept"/>). The cylinder must fit the view's width, and seen
    /// from above, its height and the near and far edges of its reach must fit the view's height; the nearer of the two
    /// distances that do so wins, so a long flat sword fills the view as well as a tall bottle does. Zoom brings the
    /// camera nearer (above 1) or further (below 1). Pure math, so the framing is testable without rendering.
    /// </summary>
    public static class TurntableFraming
    {
        /// <summary>Degrees the camera looks down on the turntable: enough to see the face of what lies flat (a sword, coins).</summary>
        public const float Pitch = 30f;

        /// <summary>Degrees a character is looked down on: about eye level, as one stands before another.</summary>
        public const float EyeLevelPitch = 5f;

        /// <summary>Room around the thing, as a fraction of what it needs.</summary>
        public const float Margin = 1.1f;

        public const float MinZoom = 0.6f;
        public const float MaxZoom = 3f;

        /// <summary>
        /// The camera's distance from the centre of a thing reaching <paramref name="reach"/> m around its axis and
        /// <paramref name="halfHeight"/> m up and down from its centre, in a view of <paramref name="verticalFov"/> degrees
        /// and <paramref name="aspect"/> (width over height), closer by <paramref name="zoom"/>, looked down on by
        /// <paramref name="pitchDegrees"/>.
        /// </summary>
        public static float Distance(float reach, float halfHeight, float verticalFov, float aspect, float zoom, float pitchDegrees = Pitch)
        {
            reach = Mathf.Max(reach, 0.01f);
            halfHeight = Mathf.Max(halfHeight, 0.01f);
            var halfVertical = verticalFov * 0.5f * Mathf.Deg2Rad;
            var halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * Mathf.Max(aspect, 0.01f));
            var pitch = pitchDegrees * Mathf.Deg2Rad;

            // Across, the reach is all that shows; up and down, the height foreshortened and the reach tilted into view.
            var across = reach / Mathf.Sin(halfHorizontal);
            var upDown = (halfHeight * Mathf.Cos(pitch) + reach * Mathf.Sin(pitch)) / Mathf.Sin(halfVertical);
            return Mathf.Max(across, upDown) * Margin / Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }

        /// <summary>
        /// The cylinder a box of half size <paramref name="extents"/> sweeps spinning around the up axis once leant by
        /// <paramref name="tilt"/>: how far its corners reach from the axis, and how far up and down from its centre.
        /// The spin doesn't change it, so a thing spinning by itself is framed steadily.
        /// </summary>
        public static void Swept(Vector3 extents, Quaternion tilt, out float reach, out float halfHeight)
        {
            reach = 0f;
            halfHeight = 0f;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = tilt * Vector3.Scale(extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                reach = Mathf.Max(reach, new Vector2(point.x, point.z).magnitude);
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(point.y));
            }
        }

        /// <summary>
        /// The camera's pose <paramref name="distance"/> m from <paramref name="centre"/>, looking at it from the front
        /// (−Z), <paramref name="pitchDegrees"/> above.
        /// </summary>
        public static Pose CameraPose(Vector3 centre, float distance, float pitchDegrees = Pitch)
        {
            var rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            return new Pose(centre - rotation * Vector3.forward * distance, rotation);
        }
    }
}
