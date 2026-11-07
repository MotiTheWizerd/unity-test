using System;
using System.Numerics;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>Raises a hill or a mountain around a point, or digs a hollow: the sculpt brush's raise and lower as one shape.</summary>
    public sealed class SculptHillCommand : EditorCommand<SculptHillCommand.Arguments, SculptResult>
    {
        readonly BuildContext _context;

        public SculptHillCommand(BuildContext context)
            : base("sculpt_hill",
                "Raises a hill or a mountain around a point, or with a negative height digs a hollow, a pit or a crater. " +
                "The ground rises by height at the center and eases into the land at its foot, width across. profile " +
                "sets the shape: round for a hill or a mound, peak for a mountain (a sharp summit, steep above, spreading " +
                "at the foot), plateau for a mesa with a flat top. roughness breaks the shape up so it looks natural. " +
                "Heights add to the ground already there, so landforms build on each other: a range is a ridge with " +
                "peaks along it. Objects on the reshaped ground move with it. The reply gives the new highest ground.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("East-west position of the center, in meters.")]
            public float X { get; set; }

            [Param("North-south position of the center, in meters.")]
            public float Z { get; set; }

            [Param("Across the foot, in meters, where it meets the land around it. Mountains are wide: a peak's sides " +
                   "are steep when the width is under about three times the height.", Minimum = 4, Maximum = LandformSculpting.MaxWidth)]
            public float Width { get; set; }

            [Param("How far the top rises above the ground there, in meters; negative digs down that deep.",
                Minimum = -WorldGrid.MaxTerrainHeight, Maximum = WorldGrid.MaxTerrainHeight)]
            public float Height { get; set; }

            [Param("The shape: \"round\" (default), \"peak\" or \"plateau\".", Optional = true)]
            public string Profile { get; set; }

            [Param("How broken the shape is: 0 smooth as a made mound, 0.3 natural (default), 1 rugged.",
                Optional = true, Minimum = 0, Maximum = 1)]
            public float? Roughness { get; set; }
        }

        protected override SculptResult Run(Arguments arguments) =>
            LandformRun.Sculpt(_context, new[] { new Vector2(arguments.X, arguments.Z) }, arguments.Width, arguments.Height,
                arguments.Profile, arguments.Roughness, landform =>
                {
                    var (verb, measure) = LandformRun.Direction(landform.Height);
                    return FormattableString.Invariant(
                        $"{verb} a {LandformRun.Words(landform.Profile)} {landform.Kind} {Math.Abs(landform.Height):0.#} m {measure} and ") +
                        FormattableString.Invariant($"{landform.Width:0.#} m across around ({arguments.X:0.#}, {arguments.Z:0.#})");
                });
    }
}
