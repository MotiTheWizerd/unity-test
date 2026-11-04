using System;
using System.Linq;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Spaces;

namespace Forge.Editing.Building
{
    /// <summary>Sets or removes the sea of the worldspace being built in: one water surface height for the whole land.</summary>
    public sealed class SetSeaLevelCommand : EditorCommand<SetSeaLevelCommand.Arguments, SetSeaLevelCommand.Result>
    {
        readonly BuildContext _context;

        public SetSeaLevelCommand(BuildContext context)
            : base("set_sea_level",
                "Sets the height of the sea's surface for the whole worldspace; all ground below it is under water, " +
                "so coasts, bays and lakes in hollows appear wherever the terrain dips below it. Leave out the level " +
                "to remove the sea. It changes the whole world, so only use it when the user asks for a sea, a coast " +
                "or a different water level. Interiors have no sea.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Height of the sea's surface, in meters. Leave out to remove the sea.", Optional = true, Minimum = 0, Maximum = WorldGrid.MaxTerrainHeight)]
            public float? Level { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, float? seaLevel, int objectsUnderWater)
            {
                Summary = summary;
                SeaLevel = seaLevel;
                ObjectsUnderWater = objectsUnderWater;
            }

            public string Summary { get; }

            /// <summary>The sea level now, in meters; null when there is no sea.</summary>
            public float? SeaLevel { get; }

            /// <summary>How many placed objects stand below the sea's surface.</summary>
            public int ObjectsUnderWater { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var space = _context.SpaceRecord;
            if (space == null)
                throw new CommandException("No space is open.");
            if (space.Kind != SpaceKind.Worldspace)
                throw new CommandException($"{space.DisplayName} is an interior, which has no sea; only worldspaces do.");

            var level = arguments.Level.HasValue ? Rounding.Meters(arguments.Level.Value) : (float?)null;
            var changed = SpaceEdits.SetWaterLevel(_context.Session, space.Id, level);
            var underWater = level is float sea ? _context.PlacedObjects().Count(p => p.Position.Y < sea) : 0;
            return new Result(Summarize(space.DisplayName, level, changed, underWater), level, underWater);
        }

        static string Summarize(string spaceName, float? level, bool changed, int underWater)
        {
            if (!level.HasValue)
                return changed ? $"Removed the sea of {spaceName}." : $"{spaceName} has no sea.";

            var summary = FormattableString.Invariant($"The sea of {spaceName} {(changed ? "is now" : "already is")} at {level.Value:0.##} m.");
            if (underWater > 0)
                summary += $" {underWater} object(s) stand below it, under water.";
            return summary + " inspect_area shows which ground is under water.";
        }
    }
}
