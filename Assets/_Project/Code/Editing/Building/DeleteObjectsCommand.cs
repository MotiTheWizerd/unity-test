using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>Removes placed objects by id, or everything (or some kinds) standing inside an area.</summary>
    public sealed class DeleteObjectsCommand : EditorCommand<DeleteObjectsCommand.Arguments, DeleteObjectsCommand.Result>
    {
        public const int MaxObjects = 1000;

        readonly BuildContext _context;

        public DeleteObjectsCommand(BuildContext context)
            : base("delete_objects",
                "Removes placed objects: by id (ids come from inspect_area or from the results of placing), or everything " +
                "standing inside an area, given as a rectangle (x, z, width, depth and heading) or points (the corners of " +
                "any shape), to clear a site or thin a forest. objects narrows either way to some kinds, e.g. only the " +
                "pines. Only placed objects can be removed.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Ids of the placed objects to remove. Instead of an area.", Optional = true, MinItems = 1, MaxItems = MaxObjects)]
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
            internal Result(string summary, int removed, IReadOnlyList<string> missing)
            {
                Summary = summary;
                Removed = removed;
                Missing = missing;
            }

            public string Summary { get; }

            public int Removed { get; }

            /// <summary>Ids that name no placed object.</summary>
            public IReadOnlyList<string> Missing { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var picked = ObjectPick.Of(_context, arguments.Ids, arguments.X, arguments.Z, arguments.Width, arguments.Depth,
                arguments.Heading, arguments.Points, arguments.Objects);
            if (picked.Objects.Count == 0)
                throw new CommandException(arguments.Ids != null
                    ? "None of those ids is a placed object of those kinds; inspect_area lists the objects in an area."
                    : $"Nothing of those kinds stands{picked.Where}.");

            var gone = _context.CountByName(picked.Objects);
            var removed = ObjectEdits.Delete(_context.Session, picked.Objects.Select(placed => placed.Id));
            var summary = $"Removed {removed} object(s){picked.Where}: {gone}.";
            if (picked.Missing.Count > 0)
                summary += $" Not found: {string.Join(", ", picked.Missing)}.";
            return new Result(summary, removed, picked.Missing);
        }
    }
}
