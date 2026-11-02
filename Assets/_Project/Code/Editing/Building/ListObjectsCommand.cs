using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;
using Forge.Editing.Commands;
using Forge.Editing.Palette;

namespace Forge.Editing.Building
{
    /// <summary>The objects that can be placed: ids, names, sizes and how placing treats each.</summary>
    public sealed class ListObjectsCommand : EditorCommand<ListObjectsCommand.Arguments, ListObjectsCommand.Result>
    {
        readonly BuildContext _context;

        public ListObjectsCommand(BuildContext context)
            : base("list_objects",
                "Lists the objects you can place: id, name, category, size at scale 1 (width across the front, " +
                "depth front to back, in meters) and how placing treats each one: whether it gets a random heading " +
                "and size, and whether it tilts to the slope.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Only objects whose name, category or id contain every word of this text, e.g. \"tree\". Leave out to list all.", Optional = true)]
            public string Search { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            public Result(string summary, IReadOnlyList<Entry> objects)
            {
                Summary = summary;
                Objects = objects;
            }

            public string Summary { get; }

            public IReadOnlyList<Entry> Objects { get; }
        }

        public sealed class Entry
        {
            internal Entry(BaseObjectRecord record, float width, float depth)
            {
                Id = record.Id.ToString();
                Name = record.DisplayName;
                Category = record.Category;
                Width = Rounding.Meters(width);
                Depth = Rounding.Meters(depth);
                var rules = record.Placement;
                RandomHeading = rules?.RandomYaw ?? false;
                MinScale = rules?.MinScale ?? 1f;
                MaxScale = rules?.MaxScale ?? 1f;
                TiltsToSlope = rules?.AlignToSlope ?? false;
            }

            public string Id { get; }

            public string Name { get; }

            public string Category { get; }

            /// <summary>Size across the object's front (its local x), at scale 1.</summary>
            public float Width { get; }

            /// <summary>Size from the object's front to its back (its local z), at scale 1.</summary>
            public float Depth { get; }

            /// <summary>New ones face a random direction unless a heading is given; in lines they keep it instead of following the path.</summary>
            public bool RandomHeading { get; }

            public float MinScale { get; }

            public float MaxScale { get; }

            public bool TiltsToSlope { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var palette = ObjectPalette.From(_context.Session.World.Records.OfType<BaseObjectRecord>());
            var shown = palette.Filter(arguments.Search);
            var entries = shown.Objects
                .Select(record =>
                {
                    var footprint = _context.Seating.ShapeOf(record).Footprint;
                    return new Entry(record, footprint.SizeX, footprint.SizeZ);
                })
                .ToList();

            var summary = ReferenceEquals(shown, palette)
                ? $"{entries.Count} objects can be placed."
                : $"{entries.Count} of {palette.Count} objects match \"{arguments.Search}\".";
            return new Result(summary, entries);
        }
    }
}
