using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data.Records;
using Forge.Editing.Commands;

namespace Forge.Editing.Building
{
    /// <summary>The ground and the objects in a square area, so a site can be chosen and checked.</summary>
    public sealed class InspectAreaCommand : EditorCommand<InspectAreaCommand.Arguments, InspectAreaCommand.Result>
    {
        /// <summary>At most this many objects are listed; the count still covers all of them.</summary>
        public const int MaxListedObjects = 200;

        const int DefaultSamples = 9;
        const float RadiansToDegrees = (float)(180.0 / Math.PI);

        readonly BuildContext _context;

        public InspectAreaCommand(BuildContext context)
            : base("inspect_area",
                "Describes a square area of the world: terrain heights on a grid (meters; rows run south to north, " +
                "columns west to east, null where there is no terrain), the lowest and highest ground, the steepest " +
                "slope, and the objects standing inside, nearest the center first.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("East-west center of the area, in meters.")]
            public float X { get; set; }

            [Param("North-south center of the area, in meters.")]
            public float Z { get; set; }

            [Param("Edge length of the square, in meters.", Minimum = 1, Maximum = 256)]
            public float Size { get; set; }

            [Param("Height samples along each edge of the grid; more shows finer detail. Default 9.", Optional = true, Minimum = 2, Maximum = 17)]
            public int? Samples { get; set; }
        }

        public sealed class Result
        {
            internal Result(string summary, TerrainInfo terrain, int objectCount, IReadOnlyList<PlacedObjectInfo> objects)
            {
                Summary = summary;
                Terrain = terrain;
                ObjectCount = objectCount;
                Objects = objects;
            }

            public string Summary { get; }

            /// <summary>Null when no part of the area has terrain.</summary>
            public TerrainInfo Terrain { get; }

            /// <summary>How many objects stand in the area (the list may be shorter).</summary>
            public int ObjectCount { get; }

            public IReadOnlyList<PlacedObjectInfo> Objects { get; }
        }

        public sealed class TerrainInfo
        {
            internal TerrainInfo(float spacing, float minHeight, float maxHeight, float steepestSlope, IReadOnlyList<IReadOnlyList<float?>> heights)
            {
                Spacing = spacing;
                MinHeight = minHeight;
                MaxHeight = maxHeight;
                SteepestSlope = steepestSlope;
                Heights = heights;
            }

            /// <summary>Meters between neighbouring samples.</summary>
            public float Spacing { get; }

            public float MinHeight { get; }

            public float MaxHeight { get; }

            /// <summary>Steepest slope between neighbouring samples, in degrees.</summary>
            public float SteepestSlope { get; }

            /// <summary>Rows south to north, columns west to east.</summary>
            public IReadOnlyList<IReadOnlyList<float?>> Heights { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var center = new Vector2(arguments.X, arguments.Z);
            var half = arguments.Size / 2f;
            var terrain = SampleTerrain(center, arguments.Size, arguments.Samples ?? DefaultSamples);

            var inside = _context.Session.World.Records.OfType<PlacedObjectRecord>()
                .Where(p => Math.Abs(p.Position.X - center.X) <= half && Math.Abs(p.Position.Z - center.Y) <= half)
                .OrderBy(p => Vector2.DistanceSquared(new Vector2(p.Position.X, p.Position.Z), center))
                .ToList();
            var listed = inside
                .Take(MaxListedObjects)
                .Select(p => new PlacedObjectInfo(p, _context.NameOf(p.BaseId)))
                .ToList();

            return new Result(Summarize(arguments, terrain, inside.Count), terrain, inside.Count, listed);
        }

        TerrainInfo SampleTerrain(Vector2 center, float size, int samples)
        {
            var spacing = size / (samples - 1);
            var origin = center - new Vector2(size / 2f, size / 2f);
            var grid = new float?[samples, samples];
            for (var row = 0; row < samples; row++)
            for (var column = 0; column < samples; column++)
                grid[row, column] = _context.Seating.TerrainHeightAt(origin + new Vector2(column * spacing, row * spacing));

            var heights = new List<IReadOnlyList<float?>>(samples);
            var known = new List<float>();
            for (var row = 0; row < samples; row++)
            {
                var line = new float?[samples];
                for (var column = 0; column < samples; column++)
                {
                    var height = grid[row, column];
                    line[column] = height.HasValue ? Rounding.Coarse(height.Value) : (float?)null;
                    if (height.HasValue)
                        known.Add(height.Value);
                }

                heights.Add(line);
            }

            if (known.Count == 0)
                return null;

            return new TerrainInfo(Rounding.Meters(spacing), Rounding.Coarse(known.Min()), Rounding.Coarse(known.Max()),
                (float)Math.Round(SteepestSlope(grid, samples, spacing)), heights);
        }

        // The steepest rise between neighbouring samples, east-west or north-south.
        static float SteepestSlope(float?[,] grid, int samples, float spacing)
        {
            var steepest = 0f;
            for (var row = 0; row < samples; row++)
            for (var column = 0; column < samples; column++)
            {
                if (!grid[row, column].HasValue)
                    continue;
                if (column + 1 < samples && grid[row, column + 1].HasValue)
                    steepest = Math.Max(steepest, Slope(grid[row, column].Value, grid[row, column + 1].Value, spacing));
                if (row + 1 < samples && grid[row + 1, column].HasValue)
                    steepest = Math.Max(steepest, Slope(grid[row, column].Value, grid[row + 1, column].Value, spacing));
            }

            return steepest;
        }

        static float Slope(float a, float b, float run) => (float)Math.Atan(Math.Abs(b - a) / run) * RadiansToDegrees;

        static string Summarize(Arguments arguments, TerrainInfo terrain, int objectCount)
        {
            var area = FormattableString.Invariant($"{arguments.Size:0.#} m square around ({arguments.X:0.#}, {arguments.Z:0.#})");
            var ground = terrain == null
                ? "no terrain (outside the world or not loaded)"
                : FormattableString.Invariant($"ground {terrain.MinHeight:0.#}–{terrain.MaxHeight:0.#} m, steepest slope {terrain.SteepestSlope:0}°");
            return $"{area}: {ground}, {objectCount} object(s).";
        }
    }
}
