using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>The grounds the terrain can be painted with.</summary>
    public sealed class ListGroundsCommand : EditorCommand<ListGroundsCommand.Arguments, ListGroundsCommand.Result>
    {
        readonly BuildContext _context;

        public ListGroundsCommand(BuildContext context)
            : base("list_grounds",
                "Lists the grounds (dirt, gravel, sand, mud…) that paint_line and paint_area lay on the terrain: id and " +
                "name. Unpainted land shows the one marked unpainted; painting that one tidies ground back to its natural look.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Only grounds whose name or id contain every word of this text, e.g. \"dirt\". Leave out to list all.", Optional = true)]
            public string Search { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, IReadOnlyList<Entry> grounds)
            {
                Summary = summary;
                Grounds = grounds;
            }

            public string Summary { get; }

            public IReadOnlyList<Entry> Grounds { get; }
        }

        public sealed class Entry
        {
            internal Entry(GroundLayerRecord record, bool unpainted)
            {
                Id = record.Id.ToString();
                Name = record.DisplayName;
                Unpainted = unpainted;
            }

            public string Id { get; }

            public string Name { get; }

            /// <summary>Whether unpainted land shows this ground.</summary>
            public bool Unpainted { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var all = GroundLayers.Of(_context.Session.World.Records);
            var shown = GroundLayers.Filter(all, arguments.Search);
            var unpainted = _context.UnpaintedGround;
            var summary = ReferenceEquals(shown, all)
                ? $"{all.Count} grounds."
                : $"{shown.Count} of {all.Count} grounds match \"{arguments.Search}\".";
            return new Result(summary, shown.Select(layer => new Entry(layer, layer.Id == unpainted)).ToList());
        }
    }
}
