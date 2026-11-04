using System;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Water;

namespace Forge.Editing.Building
{
    /// <summary>Raises or lowers a lake's surface.</summary>
    public sealed class SetLakeLevelCommand : EditorCommand<SetLakeLevelCommand.Arguments, SetLakeLevelCommand.Result>
    {
        readonly BuildContext _context;

        public SetLakeLevelCommand(BuildContext context)
            : base("set_lake_level",
                "Raises or lowers a lake's surface; its shore moves with it. It can't rise above the lowest point of its " +
                "hollow's rim (the refusal says how high it can go). Lake ids come from add_lake or inspect_area.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Id of the lake.")]
            public string Id { get; set; }

            [Param("New height of the water's surface, in meters.", Minimum = 0, Maximum = WorldGrid.MaxTerrainHeight)]
            public float Level { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, float level)
            {
                Summary = summary;
                Level = level;
            }

            public string Summary { get; }

            public float Level { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var lake = _context.LakeInSpace(arguments.Id);
            var level = Rounding.Meters(arguments.Level);
            var changed = Refusal.Of(() => WaterEdits.SetLakeLevel(_context.Session, lake.Id, level));
            return new Result(FormattableString.Invariant($"Lake {lake.Id} {(changed ? "is now" : "already is")} at {level:0.##} m."), level);
        }
    }
}
