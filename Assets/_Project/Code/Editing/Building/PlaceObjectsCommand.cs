using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Editing.Commands;
using Forge.Editing.Placement;

namespace Forge.Editing.Building
{
    /// <summary>Places objects at ground positions, each seated on the terrain the way the place tool seats it.</summary>
    public sealed class PlaceObjectsCommand : EditorCommand<PlaceObjectsCommand.Arguments, PlaceObjectsCommand.Result>
    {
        public const int MaxObjects = 200;

        readonly BuildContext _context;

        public PlaceObjectsCommand(BuildContext context)
            : base("place_objects",
                "Places objects on the ground. Give each one's x and z; it lands on the terrain by itself, upright or " +
                "tilted to the slope as its kind requires. Leave heading and scale out for the object's natural " +
                "variation (a random turn and size for trees and rocks; facing north at scale 1 for built things). " +
                "Positions without terrain are skipped. One call can place many objects of different kinds.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("The objects to place.", MinItems = 1, MaxItems = MaxObjects)]
            public List<Item> Objects { get; set; }
        }

        public sealed class Item
        {
            [Param("Id of the object to place, from list_objects.")]
            public string Object { get; set; }

            [Param("East-west position, in meters (x grows to the east).")]
            public float X { get; set; }

            [Param("North-south position, in meters (z grows to the north).")]
            public float Z { get; set; }

            [Param("Compass direction the object's front faces, in degrees: 0 north, 90 east, 180 south, 270 west.", Optional = true)]
            public float? Heading { get; set; }

            [Param("Size; 1 is as designed.", Optional = true, Minimum = 0.1, Maximum = 10)]
            public float? Scale { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, IReadOnlyList<PlacedObjectInfo> placed, IReadOnlyList<Skip> skipped)
            {
                Summary = summary;
                Placed = placed;
                Skipped = skipped;
            }

            public string Summary { get; }

            public IReadOnlyList<PlacedObjectInfo> Placed { get; }

            public IReadOnlyList<Skip> Skipped { get; }
        }

        public sealed class Skip
        {
            internal Skip(int index, string reason)
            {
                Index = index;
                Reason = reason;
            }

            /// <summary>Position of the item in the request.</summary>
            public int Index { get; }

            public string Reason { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            // Every id is checked before anything is placed, so a typo places nothing.
            var items = arguments.Objects;
            var baseObjects = items.Select(item => _context.BaseObject(item.Object)).ToList();

            var placements = new List<ObjectPlacement>(items.Count);
            var skipped = new List<Skip>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var baseObject = baseObjects[i];
                var roll = _context.Options.DrawRoll(baseObject);
                var heading = PlacementMath.NormalizeDegrees(item.Heading ?? roll.YawDegrees);
                var scale = item.Scale ?? roll.Scale;
                var pose = _context.Seating.SeatOnTerrain(baseObject, new Vector2(item.X, item.Z), heading, scale);
                if (pose.HasValue)
                    placements.Add(new ObjectPlacement(baseObject.Id, pose.Value, scale));
                else
                    skipped.Add(new Skip(i, FormattableString.Invariant($"no terrain at ({item.X:0.#}, {item.Z:0.#})")));
            }

            if (placements.Count == 0)
                throw new CommandException("Nothing was placed: none of the positions has terrain. inspect_area shows where the ground is.");

            var description = placements.Count == 1 ? $"Place {baseObjects[0].DisplayName}" : $"Place {placements.Count} objects";
            var records = ObjectEdits.Place(_context.Session, placements, description);

            var summary = $"Placed {records.Count} object(s): {_context.CountByName(records)}.";
            if (skipped.Count > 0)
                summary += $" Skipped {skipped.Count} without terrain.";
            var placed = records.Select(r => new PlacedObjectInfo(r, _context.NameOf(r.BaseId))).ToList();
            return new Result(summary, placed, skipped);
        }
    }
}
