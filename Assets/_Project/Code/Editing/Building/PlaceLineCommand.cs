using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Core.Geometry;
using Forge.Data.Records;
using Forge.Editing.Commands;
using Forge.Editing.Placement;

namespace Forge.Editing.Building
{
    /// <summary>
    /// Lays one kind of object end to end along a path, stretch by stretch, exactly as the place tool's
    /// straight line (Shift + drag) does, and seats each one on the terrain.
    /// </summary>
    public sealed class PlaceLineCommand : EditorCommand<PlaceLineCommand.Arguments, PlaceLineCommand.Result>
    {
        /// <summary>Most objects one line places; a place-tool row has the same cap.</summary>
        public const int MaxObjects = PlacementStroke.MaxItems;

        /// <summary>The farthest a row may be set from its path, in meters.</summary>
        public const float MaxOffset = 64f;

        // Where two stretches meet, the first one may end on the corner the next one starts on: one object, not two.
        const float SamePlace = 0.05f;

        readonly BuildContext _context;

        public PlaceLineCommand(BuildContext context)
            : base("place_line",
                "Lays one kind of object end to end along a path of ground points: fences, walls, hedges, rows of trees. " +
                "Objects without a random heading (fences, walls) turn to run along each stretch; others keep their " +
                "natural heading. The first object of each stretch is centered on its start point. Set closed to run " +
                "back to the first point and enclose an area; for a gate, leave the path open where the gate goes. " +
                "The gap defaults to the object's usual spacing: 0 for fences and walls, so they touch, about 1-3 m for natural things. " +
                "To line a street, give the street's middle as the path with an offset to set them that far to its side " +
                "(both_sides for a row on each side), and face_path to turn each one toward the street: lamps, signs, " +
                "barrels and crates along a road, houses fronting it.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Id of the object to lay, from list_objects.")]
            public string Object { get; set; }

            [Param("The path: ground points in meters, walked in order.", MinItems = 2, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }

            [Param("Also run from the last point back to the first, enclosing an area.", Optional = true)]
            public bool? Closed { get; set; }

            [Param("Meters left between neighbours.", Optional = true, Minimum = 0, Maximum = RowGap.Max)]
            public float? Gap { get; set; }

            [Param("Up to this many extra meters per gap, drawn at random, so rows look natural.", Optional = true, Minimum = 0, Maximum = RowGap.Max)]
            public float? RandomGap { get; set; }

            [Param("Meters to the side of the path to set them: positive to the right as the path is walked, negative to the " +
                   "left. Default 0, on the path.", Optional = true, Minimum = -MaxOffset, Maximum = MaxOffset)]
            public float? Offset { get; set; }

            [Param("Also lay the row mirrored on the other side of the path, at the same distance (needs an offset).", Optional = true)]
            public bool? BothSides { get; set; }

            [Param("Turn each one to face the path: its front toward the street.", Optional = true)]
            public bool? FacePath { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, float length, IReadOnlyList<string> ids)
            {
                Summary = summary;
                Length = length;
                Ids = ids;
            }

            public string Summary { get; }

            /// <summary>Length of the path, in meters.</summary>
            public float Length { get; }

            /// <summary>Ids of the placed objects, in path order.</summary>
            public IReadOnlyList<string> Ids { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var baseObject = _context.BaseObject(arguments.Object);
            var path = arguments.Points.Select(p => p.ToVector()).ToList();
            var closed = arguments.Closed ?? false;
            if (closed)
                path.Add(path[0]);

            var usual = _context.Options.GapFor(baseObject);
            var gap = new RowGap(arguments.Gap ?? usual.Gap, arguments.RandomGap ?? usual.RandomExtra);
            var laid = LayOut(baseObject, path, gap);
            if (closed && laid.Count > 1 && Vector2.Distance(laid[laid.Count - 1].Item.Position, laid[0].Item.Position) < SamePlace)
                laid.RemoveAt(laid.Count - 1);
            var offset = arguments.Offset ?? 0f;
            var bothSides = arguments.BothSides ?? false;
            if (bothSides && Math.Abs(offset) < SamePlace)
                throw new CommandException("both_sides needs an offset: how far to each side of the path the rows stand.");
            var items = Beside(laid, offset, bothSides, arguments.FacePath ?? false);
            if (items.Count > MaxObjects)
                throw TooMany();

            var placements = new List<ObjectPlacement>(items.Count);
            string skipReason = null;
            foreach (var item in items)
            {
                var pose = _context.Seating.SeatOnTerrain(baseObject, item.Position, item.YawDegrees, item.Scale);
                if (pose.HasValue)
                    placements.Add(new ObjectPlacement(baseObject.Id, pose.Value, item.Scale));
                else
                    skipReason = skipReason ?? _context.WhyNoGround(item.Position);
            }

            if (placements.Count == 0)
                throw new CommandException($"Nothing was placed: {skipReason}. inspect_area shows where the ground is.");

            var name = baseObject.DisplayName;
            var records = ObjectEdits.Place(_context.Session, _context.Space, placements, $"Place {placements.Count} {name}");
            var length = Length(path);
            var side = offset == 0f ? "" : FormattableString.Invariant($", {Math.Abs(offset):0.#} m to {(bothSides ? "each side" : offset > 0f ? "its right" : "its left")}");
            var facing = (arguments.FacePath ?? false) ? ", facing it" : "";
            var summary = FormattableString.Invariant($"Placed {records.Count} {name} along {length:0.#} m ({path.Count - 1} stretch(es)){side}{facing}.");
            var skipped = items.Count - placements.Count;
            if (skipped > 0)
                summary += $" Skipped {skipped}, e.g. {skipReason}.";
            return new Result(summary, Rounding.Meters(length), records.Select(r => r.Id.ToString()).ToList());
        }

        // Each laid item set offset meters to the path's right (negative: left) as walked along its stretch, and
        // mirrored to the other side too for both sides; turned to face the path when asked.
        static List<StrokeItem> Beside(IReadOnlyList<(StrokeItem Item, Vector2 Along)> laid, float offset, bool bothSides, bool facePath)
        {
            var sides = bothSides ? new[] { 1f, -1f } : new[] { 1f };
            var items = new List<StrokeItem>(laid.Count * sides.Length);
            foreach (var side in sides)
            {
                foreach (var (item, along) in laid)
                {
                    var right = new Vector2(along.Y, -along.X);
                    var away = right * (offset * side);
                    var yaw = facePath && away.LengthSquared() > 0f ? Compass.HeadingOf(-away) : item.YawDegrees;
                    items.Add(new StrokeItem(item.Position + away, yaw, item.Scale));
                }
            }

            return items;
        }

        // One straight line per stretch, the way the place tool lays a Shift line; each item with its stretch's direction.
        List<(StrokeItem Item, Vector2 Along)> LayOut(BaseObjectRecord baseObject, IReadOnlyList<Vector2> path, RowGap gap)
        {
            var options = _context.Options;
            var footprint = _context.Seating.ShapeOf(baseObject).Footprint;
            var items = new List<(StrokeItem Item, Vector2 Along)>();
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var stroke = new PlacementStroke(path[i], footprint,
                    alignToPath: !options.KeepsOwnHeading(baseObject),
                    yawOffset: 0f,
                    firstRoll: options.DrawRoll(baseObject),
                    drawRoll: () => options.DrawRoll(baseObject),
                    drawGap: () => options.DrawGap(gap));
                stroke.MoveTo(path[i + 1], line: true);
                // A full stroke stopped at its cap, short of the stretch's end.
                if (stroke.Items.Count >= PlacementStroke.MaxItems)
                    throw TooMany();

                var along = Vector2.Normalize(path[i + 1] - path[i]);
                foreach (var item in stroke.Items)
                {
                    if (items.Count > 0 && Vector2.Distance(items[items.Count - 1].Item.Position, item.Position) < SamePlace)
                        continue;
                    if (items.Count == MaxObjects)
                        throw TooMany();
                    items.Add((item, along));
                }
            }

            return items;
        }

        static CommandException TooMany() =>
            new CommandException($"That path needs more than {MaxObjects} objects; use a shorter path or a bigger gap.");

        static float Length(IReadOnlyList<Vector2> path)
        {
            var length = 0f;
            for (var i = 0; i + 1 < path.Count; i++)
                length += Vector2.Distance(path[i], path[i + 1]);
            return length;
        }
    }
}
