using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Editing.Commands;
using Forge.Editing.Geometry;
using Forge.Editing.Placement;

namespace Forge.Editing.Building
{
    /// <summary>
    /// Lays one kind of object around a rectangle (walls, fences) or over it (floor tiles), exactly as the outline
    /// tool does (<see cref="RectLayout"/>), and seats each piece on the ground.
    /// </summary>
    public sealed class PlaceOutlineCommand : EditorCommand<PlaceOutlineCommand.Arguments, PlaceOutlineCommand.Result>
    {
        public const float MaxSize = 256f;

        readonly BuildContext _context;

        public PlaceOutlineCommand(BuildContext context)
            : base("place_outline",
                "Lays one kind of object around a rectangle, or with fill over all of it. Around: walls, fences or " +
                "hedges stand end to end just inside the rectangle's edge and close neatly at every corner, so a " +
                "room or a fenced yard is one call. Each side holds (size - one piece's depth) of pieces, so for a " +
                "clean finish make width and depth a whole number of piece widths plus one piece depth (sizes from " +
                "list_objects); otherwise a side's last piece overlaps its neighbour. Fill: floor tiles, paving or " +
                "crops in rows over the rectangle; make it whole tiles. Walls around a floor of whole tiles fit when " +
                "their rectangle shares the floor's south-west corner and is one wall depth larger each way. Pieces " +
                "without a random heading run along their side (or with the rectangle); others keep their natural " +
                "heading. For a door or gate, delete the pieces where it goes, or use place_line for an open path.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Id of the object to lay, from list_objects.")]
            public string Object { get; set; }

            [Param("East-west center of the rectangle, in meters.")]
            public float X { get; set; }

            [Param("North-south center of the rectangle, in meters.")]
            public float Z { get; set; }

            [Param("Size across the rectangle's front, in meters (its outer edge).", Minimum = 0.5, Maximum = MaxSize)]
            public float Width { get; set; }

            [Param("Size from the rectangle's front to back, in meters (its outer edge).", Minimum = 0.5, Maximum = MaxSize)]
            public float Depth { get; set; }

            [Param("Compass direction the rectangle's front faces, in degrees (0 north, 90 east), e.g. a house's " +
                   "heading. Default 0.", Optional = true)]
            public float? Heading { get; set; }

            [Param("Cover the whole rectangle instead of going around it. Default false.", Optional = true)]
            public bool? Fill { get; set; }

            [Param("Meters left between neighbouring pieces. Default: the object's usual spacing, 0 for walls, " +
                   "fences and tiles.", Optional = true, Minimum = 0, Maximum = RowGap.Max)]
            public float? Gap { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, IReadOnlyList<string> ids)
            {
                Summary = summary;
                Ids = ids;
            }

            public string Summary { get; }

            /// <summary>Ids of the placed objects.</summary>
            public IReadOnlyList<string> Ids { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var baseObject = _context.BaseObject(arguments.Object);
            var options = _context.Options;
            var rect = new GroundRect(new Vector2(arguments.X, arguments.Z), arguments.Width, arguments.Depth, arguments.Heading ?? 0f);
            var pattern = arguments.Fill == true ? RectPattern.Fill : RectPattern.Outline;
            var footprint = _context.Seating.ShapeOf(baseObject).Footprint;
            var gap = arguments.Gap ?? options.GapFor(baseObject).Gap;

            var count = RectLayout.Count(rect, pattern, footprint, gap);
            if (count > RectLayout.MaxItems)
                throw new CommandException($"That needs {count} pieces, more than {RectLayout.MaxItems}; use a smaller rectangle or a bigger gap.");

            var items = RectLayout.Lay(rect, pattern, footprint, gap, !options.KeepsOwnHeading(baseObject), _ => options.DrawRoll(baseObject));
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
            var verb = RectLayout.Preposition(pattern);
            var records = ObjectEdits.Place(_context.Session, _context.Space, placements, $"Place {placements.Count} {name} {verb} a rectangle");
            var summary = FormattableString.Invariant(
                $"Placed {records.Count} {name} {verb} a {rect.Width:0.#} × {rect.Depth:0.#} m rectangle at ({rect.Center.X:0.#}, {rect.Center.Y:0.#}), heading {rect.HeadingDegrees:0}°.");
            var skipped = items.Count - placements.Count;
            if (skipped > 0)
                summary += $" Skipped {skipped}, e.g. {skipReason}.";
            return new Result(summary, records.Select(r => r.Id.ToString()).ToList());
        }
    }
}
