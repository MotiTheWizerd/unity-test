using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Forge.Editing.Geometry
{
    /// <summary>
    /// A shape on the ground (x, z) by its signed distance: how far a point lies outside the outline, negative inside
    /// (how far in from it). A band along a path (around a single point, a disc), a rectangle or a polygon. The ground
    /// commands paint, sculpt and smooth by it, so a shape means the same to each of them.
    /// </summary>
    public sealed class GroundShape
    {
        readonly Func<Vector2, float> _signedDistance;

        GroundShape(IReadOnlyList<Vector2> outline, float grow, Func<Vector2, float> signedDistance)
        {
            Min = new Vector2(outline.Min(p => p.X) - grow, outline.Min(p => p.Y) - grow);
            Max = new Vector2(outline.Max(p => p.X) + grow, outline.Max(p => p.Y) + grow);
            _signedDistance = signedDistance;
        }

        /// <summary>The south-west corner of the box around the shape.</summary>
        public Vector2 Min { get; }

        /// <summary>The north-east corner of the box around the shape.</summary>
        public Vector2 Max { get; }

        /// <summary>The box's size east-west (X) and north-south (Y).</summary>
        public Vector2 Span => Max - Min;

        /// <summary>How far <paramref name="point"/> lies outside the outline; negative inside, how far in from it.</summary>
        public float SignedDistance(Vector2 point) => _signedDistance(point);

        /// <summary>
        /// Everything within <paramref name="width"/> / 2 of the path through <paramref name="points"/>, rounded at its
        /// ends and corners; one point makes a disc <paramref name="width"/> across.
        /// </summary>
        public static GroundShape Band(IReadOnlyList<Vector2> points, float width)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("A band needs at least one point.", nameof(points));
            if (!(width > 0f))
                throw new ArgumentOutOfRangeException(nameof(width), width, "A band's width must be positive.");

            var path = points.ToArray();
            var half = width / 2f;
            return new GroundShape(path, half, point => DistanceToPath(point, path) - half);
        }

        /// <summary>
        /// The path through <paramref name="points"/> moved <paramref name="offset"/> meters to its right as it is walked
        /// (negative: to its left), staying that far from it along every stretch: at a bend the corner moves out along
        /// the bend's middle (at most <see cref="MaxMiter"/> times the offset, so a hairpin doesn't spike). A path whose
        /// last point is its first stays closed.
        /// </summary>
        public static IReadOnlyList<Vector2> OffsetPath(IReadOnlyList<Vector2> points, float offset)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("A path needs at least one point.", nameof(points));

            var count = points.Count;
            var closed = count > 2 && Vector2.DistanceSquared(points[0], points[count - 1]) < 1e-8f;
            var moved = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var before = RightOf(points, i, -1, closed);
                var after = RightOf(points, i, +1, closed);
                var right = before ?? after;
                if (right == null)
                {
                    moved[i] = points[i];
                    continue;
                }

                if (before.HasValue && after.HasValue)
                {
                    var middle = before.Value + after.Value;
                    // A path doubling straight back on itself has no middle; keep the stretch's own side.
                    if (middle.LengthSquared() > 1e-8f)
                    {
                        middle = Vector2.Normalize(middle);
                        var stretch = 1f / Math.Max(Vector2.Dot(middle, before.Value), 1f / MaxMiter);
                        moved[i] = points[i] + middle * (offset * stretch);
                        continue;
                    }
                }

                moved[i] = points[i] + right.Value * offset;
            }

            return moved;
        }

        /// <summary>How many times the offset a bend's corner may move out (a 90° bend needs √2).</summary>
        public const float MaxMiter = 3f;

        // The right-hand side of the first stretch of some length from point i going backward (-1) or forward (+1),
        // wrapping around a closed path; null when there is none.
        static Vector2? RightOf(IReadOnlyList<Vector2> points, int i, int step, bool closed)
        {
            var count = points.Count;
            // On a closed path the last point is the first, so it is skipped when wrapping.
            var ring = closed ? count - 1 : count;
            var here = points[i];
            for (var n = 1; n < count; n++)
            {
                var j = i + step * n;
                if (closed)
                    j = ((j % ring) + ring) % ring;
                else if (j < 0 || j >= count)
                    return null;

                var along = step > 0 ? points[j] - here : here - points[j];
                if (along.LengthSquared() > 1e-8f)
                {
                    along = Vector2.Normalize(along);
                    return new Vector2(along.Y, -along.X);
                }
            }

            return null;
        }

        /// <summary>The rectangle <paramref name="area"/>.</summary>
        public static GroundShape Rect(GroundRect area) => new GroundShape(area.Corners(), 0f, point => SignedDistance(area, point));

        /// <summary>
        /// The polygon with <paramref name="corners"/> (three or more, in order around it; where its sides cross, the
        /// even-odd rule decides what is inside).
        /// </summary>
        public static GroundShape Polygon(IReadOnlyList<Vector2> corners)
        {
            if (corners == null || corners.Count < 3)
                throw new ArgumentException("An area needs at least three corners.", nameof(corners));

            var ring = corners.ToArray();
            return new GroundShape(ring, 0f, point => Inside(point, ring) ? -DistanceToRing(point, ring) : DistanceToRing(point, ring));
        }

        /// <summary>Distance from <paramref name="point"/> to the path through <paramref name="points"/> (to the point, if one).</summary>
        public static float DistanceToPath(Vector2 point, IReadOnlyList<Vector2> points)
        {
            if (points.Count == 1)
                return Vector2.Distance(point, points[0]);

            var nearest = float.MaxValue;
            for (var i = 1; i < points.Count; i++)
                nearest = Math.Min(nearest, DistanceToSegment(point, points[i - 1], points[i]));
            return nearest;
        }

        // Outside: how far from the rectangle; inside, negative: how far in from its nearest side.
        static float SignedDistance(GroundRect area, Vector2 point)
        {
            var outside = area.DistanceOutside(point);
            if (outside > 0f)
                return outside;

            var offset = point - area.Center;
            var fromSides = area.Width / 2f - Math.Abs(Vector2.Dot(offset, area.Across));
            var fromEnds = area.Depth / 2f - Math.Abs(Vector2.Dot(offset, area.Front));
            return -Math.Min(fromSides, fromEnds);
        }

        // Distance to the polygon's sides, the last corner joined back to the first.
        static float DistanceToRing(Vector2 point, IReadOnlyList<Vector2> corners)
        {
            var nearest = DistanceToSegment(point, corners[corners.Count - 1], corners[0]);
            for (var i = 1; i < corners.Count; i++)
                nearest = Math.Min(nearest, DistanceToSegment(point, corners[i - 1], corners[i]));
            return nearest;
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var along = b - a;
            var lengthSquared = along.LengthSquared();
            var t = lengthSquared > 0f ? Math.Max(0f, Math.Min(1f, Vector2.Dot(point - a, along) / lengthSquared)) : 0f;
            return Vector2.Distance(point, a + along * t);
        }

        // Even-odd: a ray from the point eastwards crosses the sides an odd number of times when it is inside.
        static bool Inside(Vector2 point, IReadOnlyList<Vector2> corners)
        {
            var inside = false;
            for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
            {
                var a = corners[i];
                var b = corners[j];
                if ((a.Y > point.Y) != (b.Y > point.Y)
                    && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }

            return inside;
        }
    }
}
