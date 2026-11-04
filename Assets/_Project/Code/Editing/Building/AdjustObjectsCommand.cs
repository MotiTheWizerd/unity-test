using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Commands;
using Forge.Editing.Placement;

namespace Forge.Editing.Building
{
    /// <summary>
    /// Moves, turns and resizes placed objects by id, setting each one down on the terrain at its new spot the way
    /// placing seats it, so a layout can be corrected without removing and placing again.
    /// </summary>
    public sealed class AdjustObjectsCommand : EditorCommand<AdjustObjectsCommand.Arguments, AdjustObjectsCommand.Result>
    {
        public const int MaxObjects = 200;

        readonly BuildContext _context;

        public AdjustObjectsCommand(BuildContext context)
            : base("adjust_objects",
                "Moves, turns or resizes placed objects. For each, give its id and only what changes: a new x and z, " +
                "heading, or scale; the rest stays as it is. Each object is set down on the terrain at its spot, " +
                "upright or tilted to the slope as its kind requires, exactly as placing does. Ids come from " +
                "inspect_area or from the results of placing.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("The objects to change.", MinItems = 1, MaxItems = MaxObjects)]
            public List<Item> Objects { get; set; }
        }

        public sealed class Item
        {
            [Param("Id of the placed object.")]
            public string Id { get; set; }

            [Param("New east-west position, in meters.", Optional = true)]
            public float? X { get; set; }

            [Param("New north-south position, in meters.", Optional = true)]
            public float? Z { get; set; }

            [Param("New compass direction of the object's front, in degrees: 0 north, 90 east, 180 south, 270 west.", Optional = true)]
            public float? Heading { get; set; }

            [Param("New size; 1 is as designed.", Optional = true, Minimum = 0.1, Maximum = 10)]
            public float? Scale { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, IReadOnlyList<PlacedObjectInfo> adjusted, IReadOnlyList<Skip> skipped)
            {
                Summary = summary;
                Adjusted = adjusted;
                Skipped = skipped;
            }

            public string Summary { get; }

            /// <summary>The objects that changed, as they stand now.</summary>
            public IReadOnlyList<PlacedObjectInfo> Adjusted { get; }

            /// <summary>Objects left as they were, and why.</summary>
            public IReadOnlyList<Skip> Skipped { get; }
        }

        public sealed class Skip
        {
            internal Skip(string id, string reason)
            {
                Id = id;
                Reason = reason;
            }

            public string Id { get; }

            public string Reason { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var duplicate = arguments.Objects.GroupBy(item => item.Id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                throw new CommandException($"'{duplicate.Key}' is listed more than once; give each object once with all its changes.");

            var targets = new Dictionary<RecordId, PlacedObjectRecord>();
            var skipped = new List<Skip>();
            foreach (var item in arguments.Objects)
            {
                if (!TryAdjust(item, out var adjusted, out var reason))
                    skipped.Add(new Skip(item.Id, reason));
                else
                    targets[adjusted.Id] = adjusted;
            }

            if (targets.Count == 0)
                throw new CommandException($"Nothing was changed: {skipped[0].Reason}.");

            var changed = ObjectEdits.Transform(_context.Session, targets.Keys, placed => targets[placed.Id], "Adjust");

            var world = _context.Session.World.Records;
            var current = targets.Keys.Select(id => world.Get<PlacedObjectRecord>(id)).ToList();
            var summary = $"Adjusted {current.Count} object(s): {_context.CountByName(current)}.";
            if (changed < current.Count)
                summary += $" {current.Count - changed} of them already stood that way.";
            if (skipped.Count > 0)
                summary += $" Skipped {skipped.Count}, e.g. {skipped[0].Id}: {skipped[0].Reason}.";
            return new Result(summary, current.Select(p => new PlacedObjectInfo(p, _context.NameOf(p.BaseId))).ToList(), skipped);
        }

        // The object as it should stand: the requested changes over its current place, heading and size, seated anew.
        bool TryAdjust(Item item, out PlacedObjectRecord adjusted, out string reason)
        {
            adjusted = null;
            var world = _context.Session.World.Records;
            var placed = _context.PlacedInSpace(item.Id);
            if (placed == null)
            {
                reason = "no placed object here has that id; inspect_area lists the objects in an area";
                return false;
            }

            if (!world.TryGet(placed.BaseId, out var baseRecord) || !(baseRecord is BaseObjectRecord baseObject))
            {
                reason = $"its kind of object, {placed.BaseId}, no longer exists";
                return false;
            }

            var at = new Vector2(item.X ?? placed.Position.X, item.Z ?? placed.Position.Z);
            var heading = PlacementMath.NormalizeDegrees(item.Heading ?? PlacementMath.HeadingOf(placed.Rotation));
            var scale = item.Scale ?? placed.Scale;
            var pose = _context.Seating.SeatOnTerrain(baseObject, at, heading, scale);
            if (!pose.HasValue)
            {
                reason = _context.WhyNoGround(at);
                return false;
            }

            adjusted = placed.WithTransform(pose.Value.Position, pose.Value.Rotation, scale);
            reason = null;
            return true;
        }
    }
}
