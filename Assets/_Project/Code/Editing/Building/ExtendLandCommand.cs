using System;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Spaces;

namespace Forge.Editing.Building
{
    /// <summary>Grows the land of the worldspace being built in by rows of chunks beyond one of its sides.</summary>
    public sealed class ExtendLandCommand : EditorCommand<ExtendLandCommand.Arguments, ExtendLandCommand.Result>
    {
        readonly BuildContext _context;

        public ExtendLandCommand(BuildContext context)
            : base("extend_land",
                "Adds land to the worldspace beyond one of its sides, or all four at once: a strip of terrain chunks " +
                "(each 64 m across) along the whole side, at the world's base height, joining the existing land " +
                "smoothly (with all sides, the corners are filled too). Use it when " +
                "a build needs more room than the terrain has (the <editor> note says how far it reaches). It " +
                "changes the shape of the whole world, so only when the user asks for more land or the build won't " +
                "fit. Interiors have no land.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Which side to grow: \"north\" (+z), \"south\" (-z), \"east\" (+x), \"west\" (-x), or \"all\" for every side at once.")]
            public string Side { get; set; }

            [Param("How many rows of chunks to add on that side; each chunk is 64 m.", Minimum = 1, Maximum = SpaceEdits.MaxExtendBy)]
            public int Chunks { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, int chunksAdded, string landReach)
            {
                Summary = summary;
                ChunksAdded = chunksAdded;
                LandReach = landReach;
            }

            public string Summary { get; }

            /// <summary>How many chunks of land were added.</summary>
            public int ChunksAdded { get; }

            /// <summary>The land's reach now, e.g. "20×16 chunks (1,280 × 1,024 m)".</summary>
            public string LandReach { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var space = _context.SpaceRecord;
            if (space == null)
                throw new CommandException("No space is open.");
            if (space.Kind != SpaceKind.Worldspace)
                throw new CommandException($"{space.DisplayName} is an interior, which has no land; only worldspaces do.");
            if (!LandSides.TryParseSides(arguments.Side, out var sides))
                throw new CommandException($"'{arguments.Side}' is not a side; use north, south, east, west or all.");

            int added;
            try
            {
                added = SpaceEdits.ExtendLand(_context.Session, space.Id, sides, arguments.Chunks);
            }
            catch (ArgumentException ex)
            {
                throw new CommandException(ex.Message);
            }

            var reach = LandBounds.Of(_context.Session.World.Records, space.Id)?.ToString() ?? "no land";
            var meters = arguments.Chunks * WorldGrid.ChunkSize;
            var where = sides.Count == 1 ? sides[0].Word() : "on every side";
            var summary = added == 0
                ? $"{space.DisplayName} already has that land. It is {reach}."
                : FormattableString.Invariant($"Extended {space.DisplayName} {meters:0} m {where} ({added} chunk{(added == 1 ? "" : "s")}). The land is now {reach}.");
            return new Result(summary, added, reach);
        }
    }
}
