using System;
using System.Numerics;
using Forge.Editing.Placement;

namespace Forge.Editing.Geometry
{
    /// <summary>
    /// A rectangle on the ground (x, z), turned like a placed object: <see cref="Width"/> runs across its front
    /// and <see cref="Depth"/> from front to back, and heading 0 faces north (+z).
    /// </summary>
    public readonly struct GroundRect
    {
        public GroundRect(Vector2 center, float width, float depth, float headingDegrees = 0f)
        {
            if (!(width > 0f))
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (!(depth > 0f))
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth must be positive.");

            Center = center;
            Width = width;
            Depth = depth;
            HeadingDegrees = PlacementMath.NormalizeDegrees(headingDegrees);
        }

        public Vector2 Center { get; }

        public float Width { get; }

        public float Depth { get; }

        public float HeadingDegrees { get; }

        /// <summary>Distance from the center to the farthest corner.</summary>
        public float Reach => (float)Math.Sqrt(Width * Width + Depth * Depth) / 2f;

        /// <summary>How far <paramref name="point"/> lies outside the rectangle; 0 inside or on its edge.</summary>
        public float DistanceOutside(Vector2 point)
        {
            var across = PlacementMath.LocalXAxis(HeadingDegrees);
            // The front is a quarter turn from local +X: (sin, cos) for heading h.
            var front = new Vector2(-across.Y, across.X);
            var offset = point - Center;
            var outsideAcross = Math.Max(Math.Abs(Vector2.Dot(offset, across)) - Width / 2f, 0f);
            var outsideFront = Math.Max(Math.Abs(Vector2.Dot(offset, front)) - Depth / 2f, 0f);
            return (float)Math.Sqrt(outsideAcross * outsideAcross + outsideFront * outsideFront);
        }
    }
}
