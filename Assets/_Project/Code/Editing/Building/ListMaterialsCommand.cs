using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>The materials placed objects' slots can be dressed in.</summary>
    public sealed class ListMaterialsCommand : EditorCommand<ListMaterialsCommand.Arguments, ListMaterialsCommand.Result>
    {
        readonly BuildContext _context;

        public ListMaterialsCommand(BuildContext context)
            : base("list_materials",
                "Lists the materials (wood, stone, thatch…) that set_materials can dress a placed object's slots in: id and name.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Only materials whose name or id contain every word of this text, e.g. \"wood\". Leave out to list all.", Optional = true)]
            public string Search { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, IReadOnlyList<Entry> materials)
            {
                Summary = summary;
                Materials = materials;
            }

            public string Summary { get; }

            public IReadOnlyList<Entry> Materials { get; }
        }

        public sealed class Entry
        {
            internal Entry(MaterialRecord record)
            {
                Id = record.Id.ToString();
                Name = record.DisplayName;
            }

            public string Id { get; }

            public string Name { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var all = Materials.Of(_context.Session.World.Records);
            var shown = Materials.Filter(all, arguments.Search);
            var summary = ReferenceEquals(shown, all)
                ? $"{all.Count} materials."
                : $"{shown.Count} of {all.Count} materials match \"{arguments.Search}\".";
            return new Result(summary, shown.Select(m => new Entry(m)).ToList());
        }
    }
}
