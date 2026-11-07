using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>Paints an area by the land's shape: the paint tool's Auto, over one area, with a snow line.</summary>
    public sealed class AutoPaintCommand : EditorCommand<AutoPaintCommand.Arguments, AutoPaintCommand.Result>
    {
        public const float DefaultEdge = 8f;

        readonly BuildContext _context;

        public AutoPaintCommand(BuildContext context)
            : base("auto_paint",
                "Paints an area by the land's shape, as the paint tool's Auto paints a whole world: rock on steep ground " +
                "(from about 34°), dirt on slopes (from about 20°), sand at the sea's shore, and the unpainted ground's " +
                "own look on flat land. With a snow line, the ground above it is snow, except where it is too steep for " +
                "snow to hold. Paint a mountain, a valley or a coast this way after sculpting it. Any role's ground can " +
                "be swapped, e.g. dry grass for the flat land of a cold upland. Give either a rectangle (x, z, width, " +
                "depth and heading) or points: the corners of any shape, in order around it. Every change of ground " +
                "blends, and the area fades into the paint around it over the soft edge. Painting changes only how the " +
                "ground looks, not its height or what stands on it.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Rectangle: east-west center, in meters.", Optional = true)]
            public float? X { get; set; }

            [Param("Rectangle: north-south center, in meters.", Optional = true)]
            public float? Z { get; set; }

            [Param("Rectangle: size across its front, in meters.", Optional = true, Minimum = 1, Maximum = GroundPainting.MaxSpan)]
            public float? Width { get; set; }

            [Param("Rectangle: size from its front to back, in meters.", Optional = true, Minimum = 1, Maximum = GroundPainting.MaxSpan)]
            public float? Depth { get; set; }

            [Param("Rectangle: compass direction its front faces, in degrees (0 north, 90 east). Default 0.", Optional = true)]
            public float? Heading { get; set; }

            [Param("Any other shape: its corners, in meters, in order around it. Instead of the rectangle.", Optional = true, MinItems = 3, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }

            [Param("The height snow lies from, in meters (a sculpting reply or inspect_area gives the heights); the line " +
                   "wanders a little and blends over about 12 m. Default: no snow.", Optional = true, Minimum = 0, Maximum = WorldGrid.MaxTerrainHeight)]
            public float? SnowLine { get; set; }

            [Param("Ground for flat land, from list_grounds. Default: the unpainted ground's.", Optional = true)]
            public string Flat { get; set; }

            [Param("Ground for slopes. Default Dirt.", Optional = true)]
            public string Slope { get; set; }

            [Param("Ground for steep ground and cliffs. Default Rock.", Optional = true)]
            public string Steep { get; set; }

            [Param("Ground above the snow line. Default Snow.", Optional = true)]
            public string Snow { get; set; }

            [Param("Width of the soft edge around the area, in meters, centered on its outline; 0 for a hard edge. Default 8.",
                Optional = true, Minimum = 0, Maximum = AutoPainting.MaxEdge)]
            public float? Edge { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, int area, IReadOnlyList<GroundShare> grounds)
            {
                Summary = summary;
                Area = area;
                Grounds = grounds;
            }

            public string Summary { get; }

            /// <summary>Square meters of ground painted, soft edges included.</summary>
            public int Area { get; }

            /// <summary>How much of it each ground took, largest first.</summary>
            public IReadOnlyList<GroundShare> Grounds { get; }
        }

        /// <summary>A ground and the share of the painted area it took.</summary>
        public sealed class GroundShare
        {
            internal GroundShare(string name, int percent)
            {
                Name = name;
                Percent = percent;
            }

            public string Name { get; }

            public int Percent { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var (area, words) = GroundArea.Of(arguments.X, arguments.Z, arguments.Width, arguments.Depth, arguments.Heading, arguments.Points);
            if (!(_context.UnpaintedGround is RecordId unpainted))
                throw new CommandException("This world has no ground layer for unpainted land, so painting would change how all of it looks; nothing was painted.");

            var rules = Rules(arguments, unpainted);
            var records = _context.Session.World.Records;
            var painting = new AutoPainting(records, _context.Space, rules, AutoPaint.SeaLevelOf(records, _context.Space), unpainted);
            if (Refusal.Of(() => painting.Paint(area, arguments.Edge ?? DefaultEdge)) == 0)
                throw new CommandException("There is no land under that shape; inspect_area shows where the ground is.");
            var chunks = TerrainEdits.Commit(_context.Session, painting);

            var grounds = painting.Mix()
                .Select(share => new GroundShare(_context.NameOfGround(share.Ground), (int)Math.Round(share.Share * 100f)))
                .Where(share => share.Percent > 0)
                .ToList();
            var snow = rules.SnowLine is float line ? FormattableString.Invariant($", snow from {line:0.#} m") : "";
            var mix = string.Join(", ", grounds.Select(share => $"{share.Name} {share.Percent}%"));
            return new Result($"Auto painted {words} by the land's shape{snow}: {mix}, on {chunks} chunk(s).", painting.PaintedTexels, grounds);
        }

        // The base game's rules, with the grounds the caller swapped in; a snow line needs a ground for the snow.
        AutoPaintRules Rules(Arguments arguments, RecordId unpainted)
        {
            var records = _context.Session.World.Records;
            var flat = arguments.Flat != null ? _context.Ground(arguments.Flat).Id : unpainted;
            var standard = AutoPaintRules.OfBaseLooks(records, flat, arguments.SnowLine);
            RecordId? Role(string name, RecordId? otherwise) => name != null ? _context.Ground(name).Id : otherwise;

            var rules = new AutoPaintRules(flat, Role(arguments.Slope, standard.Slope), Role(arguments.Steep, standard.Steep),
                standard.Shore, Role(arguments.Snow, standard.Snow))
            {
                SnowLine = arguments.SnowLine,
            };
            if (rules.SnowLine.HasValue && rules.Snow == null)
                throw new CommandException("This world has no snow ground; name the ground to lay above the snow line with snow.");
            return rules;
        }
    }
}
