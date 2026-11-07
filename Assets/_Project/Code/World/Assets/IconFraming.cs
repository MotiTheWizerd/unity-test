using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Where an orthographic camera stands for an item's icon in a list, as Skyrim's and Diablo's icons show things: turned
    /// to show the face that tells what it is, and filling the square. What lies flat (a sword, coins) is looked down on
    /// from well above, so its face shows rather than its edge; what stands (a flask, an apple) is seen three-quarter on
    /// from the front, a little above. A long thing (a sword, a log) is turned so its length runs across the view, then
    /// laid along the square's diagonal, the longest line a square has. The box is fitted by its corners as the camera
    /// sees them and centred on them, so nothing is lost to the box's empty corners. Assets face +Z and stand on their
    /// pivot, axis-aligned with the box. Pure math, so the framing is testable without rendering.
    /// </summary>
    public static class IconFraming
    {
        /// <summary>Degrees the camera looks down on what lies flat: its face, with a hint of its thickness.</summary>
        public const float FlatPitch = 65f;

        /// <summary>Degrees the camera looks down on what stands: about as a hand holds it up to look.</summary>
        public const float StandingPitch = 20f;

        /// <summary>The camera's heading for a thing seen three-quarter on: from its front-right (assets face +Z).</summary>
        public const float FrontRightYaw = 220f;

        /// <summary>Degrees a long thing is turned in the view, from across to along the square's diagonal.</summary>
        public const float DiagonalRoll = 45f;

        /// <summary>How many times its next size a thing's length must be to count as long.</summary>
        public const float Long = 2f;

        /// <summary>Room around the thing, as a fraction of what it needs.</summary>
        public const float Margin = 1.08f;

        /// <summary>
        /// Whether a box of half size <paramref name="extents"/> lies flat: its height is under half of each of its
        /// sides, so a coin or a blade lying down is, an apple isn't.
        /// </summary>
        public static bool IsFlat(Vector3 extents) => extents.y < 0.5f * Mathf.Min(extents.x, extents.z);

        /// <summary>
        /// Whether a box of half size <paramref name="extents"/> is long: its longer side at least <see cref="Long"/>
        /// times what comes next (its other side, and its height unless it lies flat). Something tall isn't: it stands
        /// upright in its square.
        /// </summary>
        public static bool IsLong(Vector3 extents)
        {
            var length = Mathf.Max(extents.x, extents.z);
            var next = Mathf.Min(extents.x, extents.z);
            if (!IsFlat(extents))
                next = Mathf.Max(next, extents.y);
            return length >= Long * Mathf.Max(next, 1e-4f);
        }

        /// <summary>Which way the camera looks at a box of half size <paramref name="extents"/>.</summary>
        public static Quaternion ViewRotation(Vector3 extents)
        {
            var pitch = IsFlat(extents) ? FlatPitch : StandingPitch;
            if (!IsLong(extents))
                return Quaternion.Euler(pitch, FrontRightYaw, 0f);

            // The camera's right runs along the length (x: heading 180°, z: 90°), then it rolls the length onto the
            // diagonal, rising to the right.
            var yaw = extents.x >= extents.z ? 180f : 90f;
            return Quaternion.Euler(pitch, yaw, DiagonalRoll);
        }

        /// <summary>
        /// The camera's pose for <paramref name="bounds"/> (axis-aligned with the thing), with the half size of the square
        /// it must show and its clip planes.
        /// </summary>
        public static Pose CameraPose(Bounds bounds, out float orthographicSize, out float nearPlane, out float farPlane)
        {
            var rotation = ViewRotation(bounds.extents);
            var inverse = Quaternion.Inverse(rotation);

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (var corner = 0; corner < 8; corner++)
            {
                var local = inverse * Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            // Centred on what the camera sees of it; a square picture, so the larger half extent sets the size.
            var centre = (min + max) * 0.5f;
            orthographicSize = Mathf.Max(max.x - centre.x, max.y - centre.y, 0.005f) * Margin;
            var depth = Mathf.Max(max.z - min.z, 0.01f);
            var distance = depth + 1f;
            nearPlane = 0.01f;
            farPlane = distance + depth + 1f;
            var aim = bounds.center + rotation * new Vector3(centre.x, centre.y, 0f);
            return new Pose(aim - rotation * Vector3.forward * distance, rotation);
        }
    }
}
