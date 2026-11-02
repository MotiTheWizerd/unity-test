using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>Removes placed objects by id.</summary>
    public sealed class DeleteObjectsCommand : EditorCommand<DeleteObjectsCommand.Arguments, DeleteObjectsCommand.Result>
    {
        public const int MaxObjects = 1000;

        readonly BuildContext _context;

        public DeleteObjectsCommand(BuildContext context)
            : base("delete_objects",
                "Removes placed objects by id. Ids come from inspect_area or from the results of placing. " +
                "Only placed objects can be removed.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Ids of the placed objects to remove.", MinItems = 1, MaxItems = MaxObjects)]
            public List<string> Ids { get; set; }
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
            var world = _context.Session.World.Records;
            var present = new List<RecordId>();
            var missing = new List<string>();
            foreach (var id in arguments.Ids.Distinct())
            {
                if (RecordId.TryParse(id, out var recordId) && world.TryGet(recordId, out var record) && record is PlacedObjectRecord)
                    present.Add(recordId);
                else
                    missing.Add(id);
            }

            if (present.Count == 0)
                throw new CommandException("None of those ids is a placed object; inspect_area lists the objects in an area.");

            var removed = ObjectEdits.Delete(_context.Session, present);
            var summary = $"Removed {removed} object(s).";
            if (missing.Count > 0)
                summary += $" Not found: {string.Join(", ", missing)}.";
            return new Result(summary, removed, missing);
        }
    }
}
