using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data.Terrain;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>Raises a ridge along a path, or digs a valley: the sculpt brush's raise and lower dragged along a line, as one shape.</summary>
    public sealed class SculptRidgeCommand : EditorCommand<SculptRidgeCommand.Arguments, SculptResult>
    {
        readonly BuildContext _context;

        public SculptRidgeCommand(BuildContext context)
            : base("sculpt_ridge",
                "Raises a ridge along a path of ground points: a mountain range's spine, a line of hills, an embankment, " +
                "a crater's rim (closed). With a negative height it digs a valley, a ravine, a ditch or a river bed. The " +
                "ground rises by height along the path and eases into the land at width / 2 to each side; the ends are " +
                "rounded. profile and roughness work as for sculpt_hill; roughness also lets the crest rise and fall. " +
                "Heights add to the ground already there, so put peaks on a ridge with sculpt_hill. Objects on the " +
                "reshaped ground move with it. The reply gives the new highest ground.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("The path along the crest (or the valley floor): ground points in meters, in order.", MinItems = 2, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }

            [Param("Also run from the last point back to the first, for a ring.", Optional = true)]
            public bool? Closed { get; set; }

            [Param("Across the foot, in meters, from where it meets the land on one side to the other.",
                Minimum = 4, Maximum = LandformSculpting.MaxWidth)]
            public float Width { get; set; }

            [Param("How far the crest rises above the ground there, in meters; negative digs down that deep.",
                Minimum = -WorldGrid.MaxTerrainHeight, Maximum = WorldGrid.MaxTerrainHeight)]
            public float Height { get; set; }

            [Param("The shape across it: \"round\" (default), \"peak\" (a sharp crest, or a V valley) or \"plateau\" " +
                   "(a flat top, or a flat-bottomed valley).", Optional = true)]
            public string Profile { get; set; }

            [Param("How broken the shape is: 0 smooth as a made embankment, 0.3 natural (default), 1 rugged.",
                Optional = true, Minimum = 0, Maximum = 1)]
            public float? Roughness { get; set; }
        }

        protected override SculptResult Run(Arguments arguments)
        {
            var path = arguments.Points.Select(p => p.ToVector()).ToList();
            if (arguments.Closed ?? false)
                path.Add(path[0]);

            return LandformRun.Sculpt(_context, path, arguments.Width, arguments.Height, arguments.Profile, arguments.Roughness, landform =>
            {
                var (verb, measure) = LandformRun.Direction(landform.Height);
                return FormattableString.Invariant(
                    $"{verb} a {LandformRun.Words(landform.Profile)} {landform.Kind} {Math.Abs(landform.Height):0.#} m {measure} and ") +
                    FormattableString.Invariant($"{landform.Width:0.#} m wide along {Length(path):0.#} m");
            });
        }

        static float Length(IReadOnlyList<Vector2> path)
        {
            var length = 0f;
            for (var i = 0; i + 1 < path.Count; i++)
                length += Vector2.Distance(path[i], path[i + 1]);
            return length;
        }
    }
}
