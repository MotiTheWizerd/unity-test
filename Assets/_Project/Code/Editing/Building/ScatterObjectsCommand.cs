using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Editing.Commands;
using Forge.Editing.Placement;

namespace Forge.Editing.Building
{
    /// <summary>Scatters objects over an area by rules on the ground: a forest, rocks on a slope, bushes along a valley floor.</summary>
    public sealed class ScatterObjectsCommand : EditorCommand<ScatterObjectsCommand.Arguments, ScatterObjectsCommand.Result>
    {
        public const float DefaultClustering = 0.5f;
        public const float MaxSpan = 512f;

        readonly BuildContext _context;

        public ScatterObjectsCommand(BuildContext context)
            : base("scatter_objects",
                "Scatters objects over an area where the ground fits rules: a forest on the gentle slopes below the snow, " +
                "rocks on steep ground, reeds by water, bushes in a valley. Give the kinds (picked at random for each " +
                "spot), how many, and either a rectangle (x, z, width, depth and heading) or points: the corners of any " +
                "shape. Rules: the slope and the height the ground may have, the spacing between things, and how " +
                "clustered they are (groves with clearings, or even). They keep their spacing from what already stands " +
                "there and stay out of water unless allowed. Each lands on the terrain like place_objects, with its kind's " +
                "natural turn and size. The reply says how many fit and, if fewer than asked, which rule held them back.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Ids of the kinds to scatter, from list_objects; each spot gets one at random.", MinItems = 1, MaxItems = 8)]
            public List<string> Objects { get; set; }

            [Param("How many to place.", Minimum = 1, Maximum = PlaceObjectsCommand.MaxObjects)]
            public int Count { get; set; }

            [Param("Rectangle: east-west center, in meters.", Optional = true)]
            public float? X { get; set; }

            [Param("Rectangle: north-south center, in meters.", Optional = true)]
            public float? Z { get; set; }

            [Param("Rectangle: size across its front, in meters.", Optional = true, Minimum = 1, Maximum = MaxSpan)]
            public float? Width { get; set; }

            [Param("Rectangle: size from its front to back, in meters.", Optional = true, Minimum = 1, Maximum = MaxSpan)]
            public float? Depth { get; set; }

            [Param("Rectangle: compass direction its front faces, in degrees (0 north, 90 east). Default 0.", Optional = true)]
            public float? Heading { get; set; }

            [Param("Any other shape: its corners, in meters, in order around it. Instead of the rectangle.", Optional = true, MinItems = 3, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }

            [Param("Gentlest ground, in degrees. Default 0.", Optional = true, Minimum = 0, Maximum = 90)]
            public float? MinSlope { get; set; }

            [Param("Steepest ground, in degrees, e.g. 25 for trees. Default: any.", Optional = true, Minimum = 0, Maximum = 90)]
            public float? MaxSlope { get; set; }

            [Param("Lowest ground, in meters. Default: any.", Optional = true)]
            public float? MinHeight { get; set; }

            [Param("Highest ground, in meters, e.g. below a snow line. Default: any.", Optional = true)]
            public float? MaxHeight { get; set; }

            [Param("The least distance between two things, and from what already stands there, in meters. Default: the " +
                   "widest kind's size plus a meter.", Optional = true, Minimum = 0.5, Maximum = 64)]
            public float? Spacing { get; set; }

            [Param("0 spreads them evenly; toward 1 they gather in groves with clearings between. Default 0.5.",
                Optional = true, Minimum = 0, Maximum = 1)]
            public float? Clustering { get; set; }

            [Param("Also place under water (reeds, rocks in a stream). Default false.", Optional = true)]
            public bool? InWater { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, int placed, IReadOnlyList<string> heldBack)
            {
                Summary = summary;
                Placed = placed;
                HeldBack = heldBack;
            }

            public string Summary { get; }

            /// <summary>How many were placed.</summary>
            public int Placed { get; }

            /// <summary>When fewer fit than asked, what turned spots down, most first.</summary>
            public IReadOnlyList<string> HeldBack { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            // Every id is checked before anything is placed, so a typo places nothing.
            var kinds = arguments.Objects.Select(_context.BaseObject).ToList();
            var (area, words) = GroundArea.Of(arguments.X, arguments.Z, arguments.Width, arguments.Depth, arguments.Heading, arguments.Points);
            var rules = new ScatterRules
            {
                MinSlope = arguments.MinSlope ?? 0f,
                MaxSlope = arguments.MaxSlope ?? 90f,
                MinHeight = arguments.MinHeight,
                MaxHeight = arguments.MaxHeight,
                Spacing = arguments.Spacing ?? kinds.Max(kind => Math.Max(_context.Seating.ShapeOf(kind).Footprint.SizeX, _context.Seating.ShapeOf(kind).Footprint.SizeZ)) + 1f,
                Clustering = arguments.Clustering ?? DefaultClustering,
                DryOnly = !(arguments.InWater ?? false),
            };

            var reach = new Vector2(rules.Spacing);
            var standing = _context.PlacedObjects()
                .Select(placed => new Vector2(placed.Position.X, placed.Position.Z))
                .Where(at => at.X >= area.Min.X - reach.X && at.Y >= area.Min.Y - reach.Y && at.X <= area.Max.X + reach.X && at.Y <= area.Max.Y + reach.Y)
                .ToList();
            var random = _context.Options.Random;
            var scattering = new Scattering(area, rules, _context.Terrain, _context.Water, standing, random, arguments.Count);

            var placements = new List<ObjectPlacement>();
            foreach (var spot in scattering.Spots)
            {
                var kind = kinds[random.Next(kinds.Count)];
                var roll = _context.Options.DrawRoll(kind);
                var pose = _context.Seating.SeatOnTerrain(kind, spot, PlacementMath.NormalizeDegrees(roll.YawDegrees), roll.Scale);
                if (pose.HasValue)
                    placements.Add(new ObjectPlacement(kind.Id, pose.Value, roll.Scale));
            }

            var heldBack = HeldBack(scattering);
            if (placements.Count == 0)
                throw new CommandException($"Nothing fit {words}: {(heldBack.Count > 0 ? string.Join("; ", heldBack) : "no ground there was loaded")}. Loosen that rule, or inspect_area to see the ground.");

            var records = ObjectEdits.Place(_context.Session, _context.Space, placements, $"Scatter {placements.Count} objects");
            var summary = $"Scattered {records.Count} object(s) over {words}: {_context.CountByName(records)}.";
            if (records.Count < arguments.Count)
                summary += $" Only {records.Count} of {arguments.Count} fit; held back by {string.Join("; ", heldBack)}.";
            return new Result(summary, records.Count, records.Count < arguments.Count ? heldBack : Array.Empty<string>());
        }

        // The rules that turned spots down, most first, in words.
        static IReadOnlyList<string> HeldBack(Scattering scattering) =>
            new[]
                {
                    (scattering.TooSteepOrFlat, "the slope rule"),
                    (scattering.OutOfHeight, "the height rule"),
                    (scattering.UnderWater, "water"),
                    (scattering.Crowded, "the spacing (no room left)"),
                    (scattering.Clearing, "the clustering's clearings"),
                    (scattering.NoLand, "no land"),
                }
                .Where(reason => reason.Item1 > 0)
                .OrderByDescending(reason => reason.Item1)
                .Select(reason => $"{reason.Item2} ({reason.Item1} tries)")
                .ToList();
    }
}
