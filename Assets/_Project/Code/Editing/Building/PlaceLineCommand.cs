using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

        // Where two stretches meet, the first one may end on the corner the next one starts on: one object, not two.
        const float SamePlace = 0.05f;

        readonly BuildContext _context;

        public PlaceLineCommand(BuildContext context)
            : base("place_line",
                "Lays one kind of object end to end along a path of ground points: fences, walls, hedges, rows of trees. " +
                "Objects without a random heading (fences, walls) turn to run along each stretch; others keep their " +
                "natural heading. The first object of each stretch is centered on its start point. Set closed to run " +
                "back to the first point and enclose an area; for a gate, leave the path open where the gate goes. " +
                "The gap defaults to the object's usual spacing: 0 for fences and walls, so they touch, about 1-3 m for natural things.")
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
            var items = LayOut(baseObject, path, gap);
            if (closed && items.Count > 1 && Vector2.Distance(items[items.Count - 1].Position, items[0].Position) < SamePlace)
                items.RemoveAt(items.Count - 1);

            var placements = new List<ObjectPlacement>(items.Count);
            foreach (var item in items)
            {
                var pose = _context.Seating.SeatOnTerrain(baseObject, item.Position, item.YawDegrees, item.Scale);
                if (pose.HasValue)
                    placements.Add(new ObjectPlacement(baseObject.Id, pose.Value, item.Scale));
            }

            if (placements.Count == 0)
                throw new CommandException("Nothing was placed: the path has no terrain under it. inspect_area shows where the ground is.");

            var name = baseObject.DisplayName;
            var records = ObjectEdits.Place(_context.Session, placements, $"Place {placements.Count} {name}");
            var length = Length(path);
            var summary = FormattableString.Invariant($"Placed {records.Count} {name} along {length:0.#} m ({path.Count - 1} stretch(es)).");
            var skipped = items.Count - placements.Count;
            if (skipped > 0)
                summary += $" Skipped {skipped} without terrain.";
            return new Result(summary, Rounding.Meters(length), records.Select(r => r.Id.ToString()).ToList());
        }

        // One straight line per stretch, the way the place tool lays a Shift line.
        List<StrokeItem> LayOut(BaseObjectRecord baseObject, IReadOnlyList<Vector2> path, RowGap gap)
        {
            var options = _context.Options;
            var footprint = _context.Seating.ShapeOf(baseObject).Footprint;
            var items = new List<StrokeItem>();
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

                foreach (var item in stroke.Items)
                {
                    if (items.Count > 0 && Vector2.Distance(items[items.Count - 1].Position, item.Position) < SamePlace)
                        continue;
                    if (items.Count == MaxObjects)
                        throw TooMany();
                    items.Add(item);
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
