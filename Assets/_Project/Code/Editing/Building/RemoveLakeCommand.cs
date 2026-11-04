using System;
using Forge.Editing.Commands;
using Forge.Editing.Water;

namespace Forge.Editing.Building
{
    /// <summary>Drains a lake.</summary>
    public sealed class RemoveLakeCommand : EditorCommand<RemoveLakeCommand.Arguments, RemoveLakeCommand.Result>
    {
        readonly BuildContext _context;

        public RemoveLakeCommand(BuildContext context)
            : base("remove_lake", "Removes a lake; the hollow it filled stays. Lake ids come from add_lake or inspect_area.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Id of the lake.")]
            public string Id { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary) => Summary = summary;

            public string Summary { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var lake = _context.LakeInSpace(arguments.Id);
            Refusal.Of(() =>
            {
                WaterEdits.RemoveLake(_context.Session, lake.Id);
                return true;
            });
            return new Result($"Removed lake {lake.Id}.");
        }
    }
}
