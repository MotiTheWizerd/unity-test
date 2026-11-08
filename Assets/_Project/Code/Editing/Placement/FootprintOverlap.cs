using System;
using System.Collections.Generic;
using System.Numerics;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// Which of a set of things stand in each other's way: their footprints, turned and scaled as they stand, overlap.
    /// Two rectangles overlap unless one of their four edge directions separates them (the separating axis test).
    /// Things that only touch (fences end to end, a house against a wall) don't count: they must overlap by more than
    /// <see cref="Touching"/>.
    /// </summary>
    public static class FootprintOverlap
    {
        /// <summary>Meters two footprints may overlap and still count as touching.</summary>
        public const float Touching = 0.1f;

        /// <summary>One thing on the ground: where it stands, its footprint, its turn and size.</summary>
        public readonly struct Standing
        {
            public Standing(Vector2 position, Footprint footprint, float yawDegrees, float scale)
            {
                Position = position;
                Footprint = footprint;
                YawDegrees = yawDegrees;
                Scale = scale;
            }

            public Vector2 Position { get; }

            public Footprint Footprint { get; }

            public float YawDegrees { get; }

            public float Scale { get; }

            // Half its diagonal: nothing farther than two of these apart can overlap.
            internal float Reach => 0.5f * Scale * (float)Math.Sqrt(Footprint.SizeX * Footprint.SizeX + Footprint.SizeZ * Footprint.SizeZ);
        }

        /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> overlap by more than <see cref="Touching"/>.</summary>
        public static bool Overlaps(Standing a, Standing b)
        {
            var between = b.Position - a.Position;
            if (between.Length() >= a.Reach + b.Reach)
                return false;

            foreach (var axis in AxesOf(a.YawDegrees, b.YawDegrees))
            {
                var apart = Math.Abs(Vector2.Dot(between, axis));
                var reach = a.Footprint.HalfLengthAlong(axis, a.YawDegrees, a.Scale) + b.Footprint.HalfLengthAlong(axis, b.YawDegrees, b.Scale);
                if (apart >= reach - Touching)
                    return false;
            }

            return true;
        }

        /// <summary>The pairs (indices into <paramref name="things"/>, the lower first) that overlap, in order, at most <paramref name="max"/>.</summary>
        public static IReadOnlyList<(int A, int B)> Pairs(IReadOnlyList<Standing> things, int max)
        {
            var pairs = new List<(int, int)>();
            for (var i = 0; i < things.Count && pairs.Count < max; i++)
            for (var j = i + 1; j < things.Count && pairs.Count < max; j++)
            {
                if (Overlaps(things[i], things[j]))
                    pairs.Add((i, j));
            }

            return pairs;
        }

        static IEnumerable<Vector2> AxesOf(float yawA, float yawB)
        {
            foreach (var yaw in new[] { yawA, yawB })
            {
                var x = PlacementMath.LocalXAxis(yaw);
                yield return x;
                yield return new Vector2(-x.Y, x.X);
            }
        }
    }
}
