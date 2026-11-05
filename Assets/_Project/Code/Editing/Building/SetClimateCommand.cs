using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Commands;
using Forge.Editing.Spaces;

namespace Forge.Editing.Building
{
    /// <summary>Gives the worldspace being built in a climate (its seasons, day length and moons), or removes it.</summary>
    public sealed class SetClimateCommand : EditorCommand<SetClimateCommand.Arguments, SetClimateCommand.Result>
    {
        readonly BuildContext _context;

        public SetClimateCommand(BuildContext context)
            : base("set_climate",
                "Sets the climate of the worldspace being built in: how long its days are through the seasons (long " +
                "summer days, short winter ones), how high the sun climbs, and its moons. Leave out the climate to " +
                "remove it (then every day is the same). It changes the whole world's sky, so only use it when the " +
                "user asks for a climate, a kind of land (northern, desert) or different days. Interiors have no climate.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Name or id of the climate, e.g. \"Northern\". Leave out to remove the climate.", Optional = true)]
            public string Climate { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, string climate)
            {
                Summary = summary;
                Climate = climate;
            }

            public string Summary { get; }

            /// <summary>The id of the worldspace's climate now; null when it has none.</summary>
            public string Climate { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var space = _context.SpaceRecord;
            if (space == null)
                throw new CommandException("No space is open.");
            if (space.Kind != SpaceKind.Worldspace)
                throw new CommandException($"{space.DisplayName} is an interior, which has no climate; only worldspaces do.");

            var climate = string.IsNullOrWhiteSpace(arguments.Climate) ? null : Find(arguments.Climate);
            var changed = SpaceEdits.SetClimate(_context.Session, space.Id, climate?.Id);
            if (climate == null)
                return new Result(changed ? $"Removed the climate of {space.DisplayName}: every day is the same now." : $"{space.DisplayName} has no climate.", null);

            var summary = $"{space.DisplayName} {(changed ? "now has" : "already has")} the {climate.DisplayName} climate: " +
                          FormattableString.Invariant($"a {climate.YearLength}-day year, {climate.Summer.DaylightHours():0.#} h of daylight at midsummer and {climate.Winter.DaylightHours():0.#} h at midwinter.");
            return new Result(summary, climate.Id.ToString());
        }

        // A climate by id or by name (any case); refused with the list of climates when there is no such climate.
        ClimateRecord Find(string climate)
        {
            var climates = _context.Session.World.Records.OfType<ClimateRecord>().ToList();
            var found = RecordId.TryParse(climate, out var id)
                ? climates.FirstOrDefault(c => c.Id == id)
                : climates.FirstOrDefault(c => string.Equals(c.DisplayName, climate.Trim(), StringComparison.OrdinalIgnoreCase));
            return found ?? throw new CommandException($"There is no climate '{climate}'. The climates are: {List(climates)}.");
        }

        static string List(IReadOnlyCollection<ClimateRecord> climates) =>
            climates.Count == 0 ? "none" : string.Join(", ", climates.OrderBy(c => c.DisplayName).Select(c => $"{c.DisplayName} ({c.Id})"));
    }
}
