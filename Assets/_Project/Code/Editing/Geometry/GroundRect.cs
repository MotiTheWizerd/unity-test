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

        /// <summary>Unit vector across the front (local +X), along <see cref="Width"/>.</summary>
        public Vector2 Across => Axes().Across;

        /// <summary>Unit vector toward the front (local +Z), along <see cref="Depth"/>.</summary>
        public Vector2 Front => Axes().Front;

        /// <summary>Distance from the center to the farthest corner.</summary>
        public float Reach => (float)Math.Sqrt(Width * Width + Depth * Depth) / 2f;

        /// <summary>The four corners, going around: back-left, back-right, front-right, front-left (seen from above, facing the front).</summary>
        public Vector2[] Corners()
        {
            var (across, front) = Axes();
            var halfAcross = across * (Width / 2f);
            var halfFront = front * (Depth / 2f);
            return new[]
            {
                Center - halfAcross - halfFront,
                Center + halfAcross - halfFront,
                Center + halfAcross + halfFront,
                Center - halfAcross + halfFront,
            };
        }

        /// <summary>How far <paramref name="point"/> lies outside the rectangle; 0 inside or on its edge.</summary>
        public float DistanceOutside(Vector2 point)
        {
            var (across, front) = Axes();
            var offset = point - Center;
            var outsideAcross = Math.Max(Math.Abs(Vector2.Dot(offset, across)) - Width / 2f, 0f);
            var outsideFront = Math.Max(Math.Abs(Vector2.Dot(offset, front)) - Depth / 2f, 0f);
            return (float)Math.Sqrt(outsideAcross * outsideAcross + outsideFront * outsideFront);
        }

        // Unit vectors across the front (local +X) and toward the front, a quarter turn on: (sin, cos) for heading h.
        (Vector2 Across, Vector2 Front) Axes()
        {
            var across = PlacementMath.LocalXAxis(HeadingDegrees);
            return (across, new Vector2(-across.Y, across.X));
        }
    }
}
