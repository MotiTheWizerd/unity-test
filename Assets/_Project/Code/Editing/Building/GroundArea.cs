using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Editing.Commands;
using Forge.Editing.Geometry;

namespace Forge.Editing.Building
{
    /// <summary>
    /// The area the area commands take (paint_area, smooth_ground, auto_paint): either a rectangle (x, z, width, depth
    /// and heading, the way flatten_area takes one, so it lines up with a building) or the corners of any shape. Refused
    /// in words when it is neither or both.
    /// </summary>
    static class GroundArea
    {
        /// <returns>The area's shape, and the area in words, e.g. "a 8 × 6 m area around (30, 30), heading 0°".</returns>
        public static (GroundShape Shape, string Words) Of(float? x, float? z, float? width, float? depth, float? heading, IReadOnlyList<GroundPoint> points)
        {
            var rectangle = x.HasValue || z.HasValue || width.HasValue || depth.HasValue;
            if (rectangle == (points != null))
                throw new CommandException("Give either a rectangle (x, z, width and depth) or points, not both and not neither.");

            if (points != null)
            {
                var corners = points.Select(p => p.ToVector()).ToList();
                return (GroundShape.Polygon(corners), FormattableString.Invariant($"a {corners.Count}-cornered area of about {AreaOf(corners):0} m²"));
            }

            if (!(x.HasValue && z.HasValue && width.HasValue && depth.HasValue))
                throw new CommandException("A rectangle needs x, z, width and depth.");

            var area = new GroundRect(new Vector2(x.Value, z.Value), width.Value, depth.Value, heading ?? 0f);
            return (GroundShape.Rect(area), FormattableString.Invariant(
                $"a {area.Width:0.#} × {area.Depth:0.#} m area around ({area.Center.X:0.#}, {area.Center.Y:0.#}), heading {area.HeadingDegrees:0}°"));
        }

        // The shoelace formula: the area the corners enclose, however they wind.
        static float AreaOf(IReadOnlyList<Vector2> corners)
        {
            var twice = 0f;
            for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
                twice += corners[j].X * corners[i].Y - corners[i].X * corners[j].Y;
            return Math.Abs(twice) / 2f;
        }
    }
}
