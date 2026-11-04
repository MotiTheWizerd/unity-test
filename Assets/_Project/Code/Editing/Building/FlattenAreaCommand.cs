using System;
using System.Numerics;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Geometry;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>
    /// Levels a rectangle of ground, e.g. a building site on a hillside, blending it into the land around it.
    /// Objects on the reshaped ground move with it, as with the sculpt brush.
    /// </summary>
    public sealed class FlattenAreaCommand : EditorCommand<FlattenAreaCommand.Arguments, FlattenAreaCommand.Result>
    {
        public const float MaxSize = 128f;
        public const float DefaultBlend = 4f;

        readonly BuildContext _context;

        public FlattenAreaCommand(BuildContext context)
            : base("flatten_area",
                "Levels a rectangle of ground to one height, so buildings, fences and yards stand on flat land. Make " +
                "it a little larger than what will stand on it. The ground eases back into the natural terrain over " +
                "the blend around the rectangle, so steep sites need a wider blend to avoid a sharp bank. Objects " +
                "already standing on the reshaped ground move with it.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("East-west center of the area, in meters.")]
            public float X { get; set; }

            [Param("North-south center of the area, in meters.")]
            public float Z { get; set; }

            [Param("Size across the area's front, in meters.", Minimum = 1, Maximum = MaxSize)]
            public float Width { get; set; }

            [Param("Size from the area's front to back, in meters.", Minimum = 1, Maximum = MaxSize)]
            public float Depth { get; set; }

            [Param("Compass direction the area's front faces, in degrees (0 north, 90 east), to line it up with a " +
                   "building's heading. Default 0.", Optional = true)]
            public float? Heading { get; set; }

            [Param("Height to level to, in meters. Default: the average ground height in the area, so as much is " +
                   "dug away as filled in.", Optional = true, Minimum = 0, Maximum = WorldGrid.MaxTerrainHeight)]
            public float? Height { get; set; }

            [Param("Width of the soft edge around the area, in meters; 0 for a hard edge. Default 4.",
                Optional = true, Minimum = 0, Maximum = AreaFlattening.MaxBlend)]
            public float? Blend { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, float height, float mostRaised, float mostLowered, int movedObjects)
            {
                Summary = summary;
                Height = height;
                MostRaised = mostRaised;
                MostLowered = mostLowered;
                MovedObjects = movedObjects;
            }

            public string Summary { get; }

            /// <summary>The height the area was levelled to, in meters.</summary>
            public float Height { get; }

            /// <summary>The most any ground was raised, in meters.</summary>
            public float MostRaised { get; }

            /// <summary>The most any ground was lowered, in meters.</summary>
            public float MostLowered { get; }

            /// <summary>How many objects moved with the ground.</summary>
            public int MovedObjects { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var area = new GroundRect(new Vector2(arguments.X, arguments.Z), arguments.Width, arguments.Depth, arguments.Heading ?? 0f);
            var natural = _context.Terrain.AverageHeight(area);
            if (natural == null)
                throw new CommandException(FormattableString.Invariant(
                    $"There is no terrain at ({arguments.X:0.#}, {arguments.Z:0.#}); inspect_area shows where the ground is."));

            var height = arguments.Height ?? natural.Value;
            var flattening = new AreaFlattening(_context.Session.World.Records, _context.Space, area, height, arguments.Blend ?? DefaultBlend);
            TerrainEdits.Commit(_context.Session, flattening, _context.FollowTerrain(), out var moved);

            return new Result(Summarize(flattening, moved), Rounding.Coarse(height),
                Rounding.Coarse(flattening.MostRaised), Rounding.Coarse(flattening.MostLowered), moved);
        }

        static string Summarize(AreaFlattening flattening, int moved)
        {
            var area = flattening.Area;
            var summary =
                FormattableString.Invariant($"Levelled a {area.Width:0.#} × {area.Depth:0.#} m area around ({area.Center.X:0.#}, {area.Center.Y:0.#}), ") +
                FormattableString.Invariant($"heading {area.HeadingDegrees:0}°, to {flattening.Height:0.#} m with a {flattening.Blend:0.#} m soft edge");
            if (flattening.TouchedChunks.Count == 0)
                return summary + ": the ground was already level there.";

            summary += FormattableString.Invariant(
                $": raised the ground by up to {flattening.MostRaised:0.#} m and lowered it by up to {flattening.MostLowered:0.#} m.");
            if (moved > 0)
                summary += $" {moved} object(s) moved with the ground.";
            return summary;
        }
    }
}
