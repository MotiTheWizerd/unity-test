using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Editing.Commands;
using Forge.Editing.Geometry;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>Paints a ground over an area: a yard, a field, a square, the trodden ground inside a wall.</summary>
    public sealed class PaintAreaCommand : EditorCommand<PaintAreaCommand.Arguments, PaintAreaCommand.Result>
    {
        readonly BuildContext _context;

        public PaintAreaCommand(BuildContext context)
            : base("paint_area",
                "Paints a ground (from list_grounds) over an area: a yard, a field, a village square, the trodden ground " +
                "inside a wall. Give either a rectangle (x, z, width, depth and heading, the way flatten_area takes one, so " +
                "a yard lines up with its house) or points: the corners of any shape, in order around it. The outline fades " +
                "over the soft edge. Painting changes only how the ground looks, not its height or what stands on it.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Id or name of the ground to paint, from list_grounds, e.g. \"Gravel\".")]
            public string Ground { get; set; }

            [Param("Rectangle: east-west center, in meters.", Optional = true)]
            public float? X { get; set; }

            [Param("Rectangle: north-south center, in meters.", Optional = true)]
            public float? Z { get; set; }

            [Param("Rectangle: size across its front, in meters.", Optional = true, Minimum = 1, Maximum = GroundPainting.MaxSpan)]
            public float? Width { get; set; }

            [Param("Rectangle: size from its front to back, in meters.", Optional = true, Minimum = 1, Maximum = GroundPainting.MaxSpan)]
            public float? Depth { get; set; }

            [Param("Rectangle: compass direction its front faces, in degrees (0 north, 90 east), to line it up with a " +
                   "building's heading. Default 0.", Optional = true)]
            public float? Heading { get; set; }

            [Param("Any other shape: its corners, in meters, in order around it. Instead of the rectangle.", Optional = true, MinItems = 3, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }

            [Param("Width of the soft edge around the outline, in meters, centered on it; 0 for a hard edge. Default 1.",
                Optional = true, Minimum = 0, Maximum = GroundPainting.MaxEdge)]
            public float? Edge { get; set; }

            [Param("How fully the ground takes the new look: 1 covers it; less lets the old ground show through, for " +
                   "worn or patchy ground. Default 1.", Optional = true, Minimum = 0.05, Maximum = 1)]
            public float? Strength { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, int area)
            {
                Summary = summary;
                Area = area;
            }

            public string Summary { get; }

            /// <summary>Square meters of ground painted, soft edges included.</summary>
            public int Area { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var edge = arguments.Edge ?? GroundPaintRun.DefaultEdge;
            var strength = arguments.Strength ?? 1f;
            var rectangle = arguments.X.HasValue || arguments.Z.HasValue || arguments.Width.HasValue || arguments.Depth.HasValue;
            if (rectangle == (arguments.Points != null))
                throw new CommandException("Give either a rectangle (x, z, width and depth) or points, not both and not neither.");

            if (arguments.Points != null)
            {
                var corners = arguments.Points.Select(p => p.ToVector()).ToList();
                var (polygon, polygonChunks) = GroundPaintRun.Paint(_context, arguments.Ground,
                    shape => shape.PaintPolygon(corners, edge, strength));
                return new Result(FormattableString.Invariant(
                    $"Painted {polygon.LayerName} over a {corners.Count}-cornered area of about {Area(corners):0} m², on {polygonChunks} chunk(s)."),
                    polygon.PaintedTexels);
            }

            if (!(arguments.X.HasValue && arguments.Z.HasValue && arguments.Width.HasValue && arguments.Depth.HasValue))
                throw new CommandException("A rectangle needs x, z, width and depth.");

            var area = new GroundRect(new Vector2(arguments.X.Value, arguments.Z.Value), arguments.Width.Value, arguments.Depth.Value, arguments.Heading ?? 0f);
            var (painting, chunks) = GroundPaintRun.Paint(_context, arguments.Ground, shape => shape.PaintRect(area, edge, strength));
            return new Result(
                FormattableString.Invariant($"Painted {painting.LayerName} over a {area.Width:0.#} × {area.Depth:0.#} m area ") +
                FormattableString.Invariant($"around ({area.Center.X:0.#}, {area.Center.Y:0.#}), heading {area.HeadingDegrees:0}°, on {chunks} chunk(s)."),
                painting.PaintedTexels);
        }

        // The shoelace formula: the area the corners enclose, however they wind.
        static float Area(IReadOnlyList<Vector2> corners)
        {
            var twice = 0f;
            for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
                twice += corners[j].X * corners[i].Y - corners[i].X * corners[j].Y;
            return Math.Abs(twice) / 2f;
        }
    }
}
