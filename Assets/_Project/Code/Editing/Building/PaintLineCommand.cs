using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Editing.Commands;
using Forge.Editing.Geometry;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>Paints a ground along a path: lanes, footpaths, tracks, a ditch's mud.</summary>
    public sealed class PaintLineCommand : EditorCommand<PaintLineCommand.Arguments, PaintLineCommand.Result>
    {
        public const float DefaultWidth = 3f;
        public const float MaxWidth = 32f;
        public const float MaxOffset = 32f;

        readonly BuildContext _context;

        public PaintLineCommand(BuildContext context)
            : base("paint_line",
                "Paints a ground (from list_grounds) along a path of ground points: a lane through a village, a footpath " +
                "to a door, a track out of a gate. The band is width meters wide, rounded at its ends and corners, and its " +
                "sides fade over the soft edge. Painting changes only how the ground looks, not its height or what stands " +
                "on it. Set closed to run back to the first point (a ring road, a path around a pond). With an offset the " +
                "band runs beside the path instead of on it (wheel ruts, a strip along a street's edge), and both_sides " +
                "paints it on each side.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Id or name of the ground to paint, from list_grounds, e.g. \"Dirt\".")]
            public string Ground { get; set; }

            [Param("The path: ground points in meters, in order.", MinItems = 2, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }

            [Param("Also run from the last point back to the first.", Optional = true)]
            public bool? Closed { get; set; }

            [Param("Width of the band, in meters. Default 3.", Optional = true, Minimum = 1, Maximum = MaxWidth)]
            public float? Width { get; set; }

            [Param("Width of the soft edge along each side, in meters, centered on it; 0 for a hard edge. Default 1.",
                Optional = true, Minimum = 0, Maximum = GroundPainting.MaxEdge)]
            public float? Edge { get; set; }

            [Param("How fully the ground takes the new look: 1 covers it; less lets the old ground show through, for " +
                   "worn or patchy ground. Default 1.", Optional = true, Minimum = 0.05, Maximum = 1)]
            public float? Strength { get; set; }

            [Param("Meters to the side of the path to run the band's middle: positive to the right as the path is walked, " +
                   "negative to the left. Default 0, on the path.", Optional = true, Minimum = -MaxOffset, Maximum = MaxOffset)]
            public float? Offset { get; set; }

            [Param("Also paint the band mirrored on the other side of the path, at the same distance (needs an offset).", Optional = true)]
            public bool? BothSides { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, float length, int area)
            {
                Summary = summary;
                Length = length;
                Area = area;
            }

            public string Summary { get; }

            /// <summary>Length of the path, in meters.</summary>
            public float Length { get; }

            /// <summary>Square meters of ground painted, soft edges included.</summary>
            public int Area { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var path = arguments.Points.Select(p => p.ToVector()).ToList();
            if (arguments.Closed ?? false)
                path.Add(path[0]);

            var width = arguments.Width ?? DefaultWidth;
            var edge = arguments.Edge ?? GroundPaintRun.DefaultEdge;
            var offset = arguments.Offset ?? 0f;
            var bothSides = arguments.BothSides ?? false;
            if (bothSides && Math.Abs(offset) < 0.01f)
                throw new CommandException("both_sides needs an offset: how far to each side of the path the bands run.");
            var bands = (bothSides ? new[] { offset, -offset } : new[] { offset })
                .Select(o => o == 0f ? path : GroundShape.OffsetPath(path, o))
                .ToList();
            var (painting, chunks) = GroundPaintRun.Paint(_context, arguments.Ground,
                shape => bands.Sum(band => shape.Paint(GroundShape.Band(band, width), edge, arguments.Strength ?? 1f)));

            var length = Length(path);
            var side = offset == 0f ? "" : FormattableString.Invariant($", {Math.Abs(offset):0.#} m to {(bothSides ? "each side" : offset > 0f ? "its right" : "its left")}");
            var summary = FormattableString.Invariant(
                $"Painted {painting.LayerName} along {length:0.#} m, {width:0.#} m wide{side}, on {chunks} chunk(s).");
            return new Result(summary, Rounding.Meters(length), painting.PaintedTexels);
        }

        static float Length(IReadOnlyList<Vector2> path)
        {
            var length = 0f;
            for (var i = 0; i + 1 < path.Count; i++)
                length += Vector2.Distance(path[i], path[i + 1]);
            return length;
        }
    }
}
