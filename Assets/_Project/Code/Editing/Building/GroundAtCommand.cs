using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>The ground's exact height, slope and water at chosen points: what inspect_area's coarse grid can't say.</summary>
    public sealed class GroundAtCommand : EditorCommand<GroundAtCommand.Arguments, GroundAtCommand.Result>
    {
        readonly BuildContext _context;

        public GroundAtCommand(BuildContext context)
            : base("ground_at",
                "Gives the ground's exact height, slope and water depth at the points you name, to check spots before " +
                "placing on them (is this flat enough for a house, is this tree above the snow line). For an overview of " +
                "an area use inspect_area.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("The points to read, in meters.", MinItems = 1, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, IReadOnlyList<Ground> points)
            {
                Summary = summary;
                Points = points;
            }

            public string Summary { get; }

            public IReadOnlyList<Ground> Points { get; }
        }

        /// <summary>The ground at one point; height and slope null where there is no land.</summary>
        public sealed class Ground
        {
            internal Ground(float x, float z, float? height, float? slope, float? waterDepth)
            {
                X = x;
                Z = z;
                Height = height;
                Slope = slope;
                WaterDepth = waterDepth;
            }

            public float X { get; }

            public float Z { get; }

            /// <summary>Meters.</summary>
            public float? Height { get; }

            /// <summary>Degrees.</summary>
            public float? Slope { get; }

            /// <summary>Meters of water over the ground, when it is under the sea or a lake.</summary>
            public float? WaterDepth { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var terrain = _context.Terrain;
            var water = _context.Water;
            var points = arguments.Points.Select(point =>
            {
                var at = point.ToVector();
                var height = terrain.HeightAt(at);
                var depth = water.DepthAt(at);
                return new Ground(point.X, point.Z,
                    height.HasValue ? Rounding.Meters(height.Value) : (float?)null,
                    terrain.SlopeAt(at) is float slope ? Rounding.Degrees(slope) : (float?)null,
                    depth is float d && d > 0f ? Rounding.Meters(d) : (float?)null);
            }).ToList();

            var land = points.Count(p => p.Height.HasValue);
            var summary = land == points.Count
                ? $"Read the ground at {points.Count} point(s)."
                : $"Read the ground at {points.Count} point(s); {points.Count - land} have no land.";
            return new Result(summary, points);
        }
    }
}
