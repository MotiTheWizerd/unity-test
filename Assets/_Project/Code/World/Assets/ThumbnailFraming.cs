using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Where an orthographic camera stands to show a bounding box whole: a fixed three-quarter view from above-front,
    /// sized to the box's projected extent plus a margin. Pure math, so the framing is testable without rendering.
    /// </summary>
    public static class ThumbnailFraming
    {
        /// <summary>The view direction: from the front-right, a little above (Forge assets face +Z).</summary>
        public static readonly Vector3 ViewDirection = new Vector3(-0.55f, -0.5f, -0.67f).normalized;

        /// <summary>Room around the object, as a fraction of its projected half extent.</summary>
        public const float Margin = 1.12f;

        public static Pose CameraPose(Bounds bounds, out float orthographicSize, out float nearPlane, out float farPlane)
        {
            var rotation = Quaternion.LookRotation(ViewDirection, Vector3.up);
            var inverse = Quaternion.Inverse(rotation);

            // The box's corners in view space tell the half width and height the view must cover, and the depth range.
            var halfWidth = 0f;
            var halfHeight = 0f;
            var minDepth = float.MaxValue;
            var maxDepth = float.MinValue;
            foreach (var corner in Corners(bounds))
            {
                var local = inverse * (corner - bounds.center);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
                minDepth = Mathf.Min(minDepth, local.z);
                maxDepth = Mathf.Max(maxDepth, local.z);
            }

            // A square picture: the larger half extent sets the size. An empty box still gets a sane frame.
            orthographicSize = Mathf.Max(halfWidth, halfHeight, 0.01f) * Margin;
            var distance = Mathf.Max(maxDepth - minDepth, 0.01f) + 1f;
            nearPlane = 0.01f;
            farPlane = distance + (maxDepth - minDepth) + 1f;
            return new Pose(bounds.center - ViewDirection * distance, rotation);
        }

        static Vector3[] Corners(Bounds b)
        {
            var min = b.min;
            var max = b.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z),
            };
        }
    }
}
