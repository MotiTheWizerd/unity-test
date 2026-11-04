using System;
using System.Linq;
using System.Numerics;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Water;

namespace Forge.Editing.Building
{
    /// <summary>Fills a hollow in the ground with water, like a paint bucket: a lake or pond up to a level.</summary>
    public sealed class AddLakeCommand : EditorCommand<AddLakeCommand.Arguments, AddLakeCommand.Result>
    {
        readonly BuildContext _context;

        public AddLakeCommand(BuildContext context)
            : base("add_lake",
                "Fills the hollow around a point with water: a lake or pond. The water covers all ground below its level " +
                "that connects to the point, and follows the shore when the land is reshaped. The point must be in a " +
                "hollow (dig one with flatten_area to a lower height first if there is none), and the level can't be " +
                "higher than the lowest point of the hollow's rim, or the water would run off; a refusal says how high " +
                "it can go. Use it for lakes and ponds above the sea; ground below the sea level is under water already.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("East-west position of a point in the hollow, in meters.")]
            public float X { get; set; }

            [Param("North-south position of a point in the hollow, in meters.")]
            public float Z { get; set; }

            [Param("Height of the water's surface, in meters. Default: filled to just below the hollow's rim.",
                Optional = true, Minimum = 0, Maximum = WorldGrid.MaxTerrainHeight)]
            public float? Level { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, string id, float level, float rim, float deepest, float area, int objectsUnderWater)
            {
                Summary = summary;
                Id = id;
                Level = level;
                Rim = rim;
                Deepest = deepest;
                Area = area;
                ObjectsUnderWater = objectsUnderWater;
            }

            public string Summary { get; }

            /// <summary>The new lake's id, for set_lake_level and remove_lake.</summary>
            public string Id { get; }

            /// <summary>Height of the water's surface, in meters.</summary>
            public float Level { get; }

            /// <summary>The highest the water can stand there, in meters.</summary>
            public float Rim { get; }

            /// <summary>The deepest water, in meters.</summary>
            public float Deepest { get; }

            /// <summary>Water surface, in square meters.</summary>
            public float Area { get; }

            /// <summary>Placed objects now standing in the lake, below its surface.</summary>
            public int ObjectsUnderWater { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var point = new Vector2(arguments.X, arguments.Z);
            var at = FormattableString.Invariant($"({point.X:0.#}, {point.Y:0.#})");
            if (!(LakeFill.Survey(_context.Terrain, point) is LakeBasin basin))
                throw new CommandException($"There is no terrain at {at}; inspect_area shows where the ground is.");
            if (!basin.HoldsWater)
                throw new CommandException($"The ground at {at} isn't in a hollow, so water there would run off. Dig a hollow first, " +
                                           "e.g. flatten_area to a height a few meters below the ground around it.");

            var level = Rounding.Meters(arguments.Level ?? basin.FullLevel);
            var lake = Refusal.Of(() => WaterEdits.AddLake(_context.Session, _context.Space, point, level));

            var water = _context.Water.Lakes.First(l => l.Record.Id == lake.Id);
            var spacing = _context.Terrain.Spacing;
            var underWater = _context.PlacedObjects().Count(p =>
                p.Position.Y < level && _context.Water.LakeAt(new Vector2(p.Position.X, p.Position.Z))?.Record.Id == lake.Id);
            var summary = FormattableString.Invariant(
                $"Added lake {lake.Id} at {at}: surface at {level:0.##} m (it can rise to {basin.Brim:0.##} m), up to {water.Extent.Deepest:0.#} m deep, ") +
                FormattableString.Invariant($"about {water.Extent.SampleCount * spacing * spacing:0} m² of water.") +
                (underWater > 0 ? $" {underWater} object(s) now stand in it, under water." : string.Empty);
            return new Result(summary, lake.Id.ToString(), level, Rounding.Meters(basin.Brim), Rounding.Coarse(water.Extent.Deepest),
                (float)Math.Round(water.Extent.SampleCount * spacing * spacing), underWater);
        }
    }
}
