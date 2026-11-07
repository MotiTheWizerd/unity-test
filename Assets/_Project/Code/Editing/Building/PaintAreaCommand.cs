using System;
using System.Collections.Generic;
using Forge.Editing.Commands;
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
            var (area, words) = GroundArea.Of(arguments.X, arguments.Z, arguments.Width, arguments.Depth, arguments.Heading, arguments.Points);
            var edge = arguments.Edge ?? GroundPaintRun.DefaultEdge;
            var (painting, chunks) = GroundPaintRun.Paint(_context, arguments.Ground, shape => shape.Paint(area, edge, arguments.Strength ?? 1f));
            return new Result($"Painted {painting.LayerName} over {words}, on {chunks} chunk(s).", painting.PaintedTexels);
        }
    }
}
