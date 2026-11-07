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
    /// <summary>Moves placed objects together by an offset: by id, or everything (or some kinds) inside an area.</summary>
    public sealed class MoveObjectsCommand : EditorCommand<MoveObjectsCommand.Arguments, MoveObjectsCommand.Result>
    {
        readonly BuildContext _context;

        public MoveObjectsCommand(BuildContext context)
            : base("move_objects",
                "Moves placed objects together by an offset, keeping how they stand to each other: by id, or everything " +
                "standing inside an area, given as a rectangle (x, z, width, depth and heading) or points (the corners of " +
                "any shape): a farm moved east, items carried off a hill before it is sculpted. objects narrows either " +
                "way to some kinds. Each is set down on the terrain at its new spot, keeping its heading and size; those " +
                "whose new spot has no ground stay where they are.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("How far to move them east (negative: west), in meters.")]
            public float Dx { get; set; }

            [Param("How far to move them north (negative: south), in meters.")]
            public float Dz { get; set; }

            [Param("Ids of the placed objects to move. Instead of an area.", Optional = true, MinItems = 1, MaxItems = DeleteObjectsCommand.MaxObjects)]
            public List<string> Ids { get; set; }

            [Param("Area rectangle: east-west center, in meters.", Optional = true)]
            public float? X { get; set; }

            [Param("Area rectangle: north-south center, in meters.", Optional = true)]
            public float? Z { get; set; }

            [Param("Area rectangle: size across its front, in meters.", Optional = true, Minimum = 0.1)]
            public float? Width { get; set; }

            [Param("Area rectangle: size from its front to back, in meters.", Optional = true, Minimum = 0.1)]
            public float? Depth { get; set; }

            [Param("Area rectangle: compass direction its front faces, in degrees. Default 0.", Optional = true)]
            public float? Heading { get; set; }

            [Param("Area of any other shape: its corners, in meters, in order around it.", Optional = true, MinItems = 3, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }

            [Param("Only these kinds (ids from list_objects). Default: every kind.", Optional = true, MinItems = 1, MaxItems = 32)]
            public List<string> Objects { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, int moved, int stayed, IReadOnlyList<string> missing)
            {
                Summary = summary;
                Moved = moved;
                Stayed = stayed;
                Missing = missing;
            }

            public string Summary { get; }

            public int Moved { get; }

            /// <summary>How many stayed because their new spot has no ground.</summary>
            public int Stayed { get; }

            /// <summary>Ids that name no placed object.</summary>
            public IReadOnlyList<string> Missing { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            if (arguments.Dx == 0f && arguments.Dz == 0f)
                throw new CommandException("An offset of 0 moves nothing: give dx and dz in meters.");

            var picked = ObjectPick.Of(_context, arguments.Ids, arguments.X, arguments.Z, arguments.Width, arguments.Depth,
                arguments.Heading, arguments.Points, arguments.Objects);
            if (picked.Objects.Count == 0)
                throw new CommandException(arguments.Ids != null
                    ? "None of those ids is a placed object of those kinds; inspect_area lists the objects in an area."
                    : $"Nothing of those kinds stands{picked.Where}.");

            var world = _context.Session.World.Records;
            var offset = new Vector2(arguments.Dx, arguments.Dz);
            var targets = new Dictionary<RecordId, PlacedObjectRecord>();
            foreach (var placed in picked.Objects)
            {
                if (!(world.TryGet(placed.BaseId, out var record) && record is BaseObjectRecord baseObject))
                    continue;
                var at = new Vector2(placed.Position.X, placed.Position.Z) + offset;
                var pose = _context.Seating.SeatOnTerrain(baseObject, at, PlacementMath.HeadingOf(placed.Rotation), placed.Scale);
                if (pose.HasValue)
                    targets[placed.Id] = placed.WithTransform(pose.Value.Position, pose.Value.Rotation, placed.Scale);
            }

            var stayed = picked.Objects.Count - targets.Count;
            if (targets.Count == 0)
                throw new CommandException(FormattableString.Invariant(
                    $"Nothing was moved: {arguments.Dx:0.#} m east and {arguments.Dz:0.#} m north of them there is no ground."));

            ObjectEdits.Transform(_context.Session, targets.Keys, placed => targets[placed.Id], "Move");
            var moved = targets.Keys.Select(id => _context.Session.World.Records.Get<PlacedObjectRecord>(id)).ToList();
            var summary = FormattableString.Invariant($"Moved {moved.Count} object(s){picked.Where} by ({arguments.Dx:0.#}, {arguments.Dz:0.#}) m: ") +
                          $"{_context.CountByName(moved)}.";
            if (stayed > 0)
                summary += $" {stayed} stayed: their new spot has no ground.";
            if (picked.Missing.Count > 0)
                summary += $" Not found: {string.Join(", ", picked.Missing)}.";
            return new Result(summary, moved.Count, stayed, picked.Missing);
        }
    }
}
