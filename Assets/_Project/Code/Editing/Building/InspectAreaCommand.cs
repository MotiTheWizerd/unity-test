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
                "Describes a square area of the world: the lowest and highest ground and the steepest slope (read from " +
                "every terrain sample in the area), an overview grid of heights (meters; rows run south to north, " +
                "columns west to east, null where there is no terrain), the water if the world has a sea (its level, " +
                "how much of the area is under water and how deep), and the objects standing inside, nearest the center first.")
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

            [Param("Points along each edge of the overview grid; more shows finer detail. Default 9.", Optional = true, Minimum = 2, Maximum = 17)]
            public int? Samples { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, TerrainInfo terrain, WaterInfo water, int objectCount, IReadOnlyList<PlacedObjectInfo> objects)
            {
                Summary = summary;
                Terrain = terrain;
                Water = water;
                ObjectCount = objectCount;
                Objects = objects;
            }

            public string Summary { get; }

            /// <summary>Null when no part of the area has terrain.</summary>
            public TerrainInfo Terrain { get; }

            /// <summary>Null when the world has no sea or the area no terrain.</summary>
            public WaterInfo Water { get; }

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

            /// <summary>Meters between neighbouring points of the overview grid.</summary>
            public float Spacing { get; }

            public float MinHeight { get; }

            public float MaxHeight { get; }

            /// <summary>Steepest slope between neighbouring terrain samples, in degrees.</summary>
            public float SteepestSlope { get; }

            /// <summary>The overview grid: rows south to north, columns west to east.</summary>
            public IReadOnlyList<IReadOnlyList<float?>> Heights { get; }
        }

        public sealed class WaterInfo
        {
            internal WaterInfo(float seaLevel, int underwaterPercent, float deepest)
            {
                SeaLevel = seaLevel;
                UnderwaterPercent = underwaterPercent;
                Deepest = deepest;
            }

            /// <summary>Height of the sea's surface, in meters.</summary>
            public float SeaLevel { get; }

            /// <summary>How much of the area's ground is under water, in percent of its terrain samples.</summary>
            public int UnderwaterPercent { get; }

            /// <summary>The deepest water in the area, in meters; 0 when it is all dry.</summary>
            public float Deepest { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var center = new Vector2(arguments.X, arguments.Z);
            var half = arguments.Size / 2f;
            var terrain = DescribeTerrain(center, arguments.Size, arguments.Samples ?? DefaultSamples);
            var water = terrain != null ? DescribeWater(center, half) : null;

            var inside = _context.PlacedObjects()
                .Where(p => Math.Abs(p.Position.X - center.X) <= half && Math.Abs(p.Position.Z - center.Y) <= half)
                .OrderBy(p => Vector2.DistanceSquared(new Vector2(p.Position.X, p.Position.Z), center))
                .ToList();
            var listed = inside
                .Take(MaxListedObjects)
                .Select(p => new PlacedObjectInfo(p, _context.NameOf(p.BaseId)))
                .ToList();

            return new Result(Summarize(arguments, terrain, water, inside.Count), terrain, water, inside.Count, listed);
        }

        // Like the ground's extremes, the share under water and the deepest point come from every terrain sample.
        WaterInfo DescribeWater(Vector2 center, float half)
        {
            var water = _context.Water;
            if (!(water.SeaLevel is float seaLevel))
                return null;

            var samples = 0;
            var underwater = 0;
            var deepest = 0f;
            foreach (var sample in _context.Terrain.SamplesAround(center, half))
            {
                samples++;
                if (!(water.DepthAt(sample.Ground) is float depth) || depth <= 0f)
                    continue;
                underwater++;
                deepest = Math.Max(deepest, depth);
            }

            // An area narrower than the sample spacing may hold no sample at all; its center still has a depth.
            if (samples == 0 && water.DepthAt(center) is float middle)
            {
                samples = 1;
                underwater = middle > 0f ? 1 : 0;
                deepest = middle;
            }

            var percent = samples > 0 ? (int)Math.Round(100.0 * underwater / samples) : 0;
            // Any water at all shows as at least 1%, so a sliver of shore is never reported as dry.
            if (underwater > 0 && percent == 0)
                percent = 1;
            return new WaterInfo(Rounding.Coarse(seaLevel), percent, Rounding.Coarse(deepest));
        }

        // Lowest, highest and steepest ground come from every terrain sample in the square, so they are exact
        // however coarse the overview grid is; the overview grid only shows the lay of the land.
        TerrainInfo DescribeTerrain(Vector2 center, float size, int points)
        {
            var terrain = _context.Terrain;
            var half = size / 2f;
            var spacing = terrain.Spacing;
            var firstX = (int)Math.Ceiling((center.X - half) / spacing);
            var lastX = (int)Math.Floor((center.X + half) / spacing);
            var firstZ = (int)Math.Ceiling((center.Y - half) / spacing);
            var lastZ = (int)Math.Floor((center.Y + half) / spacing);

            var lowest = float.MaxValue;
            var highest = float.MinValue;
            var steepest = 0f;
            for (var gz = firstZ; gz <= lastZ; gz++)
            for (var gx = firstX; gx <= lastX; gx++)
            {
                if (!(terrain.SampleHeight(gx, gz) is float height))
                    continue;
                lowest = Math.Min(lowest, height);
                highest = Math.Max(highest, height);
                if (gx < lastX && terrain.SampleHeight(gx + 1, gz) is float east)
                    steepest = Math.Max(steepest, Slope(height, east, spacing));
                if (gz < lastZ && terrain.SampleHeight(gx, gz + 1) is float north)
                    steepest = Math.Max(steepest, Slope(height, north, spacing));
            }

            // An area narrower than the sample spacing may hold no sample at all; its center still has a height.
            if (lowest > highest && terrain.HeightAt(center) is float middle)
                lowest = highest = middle;
            if (lowest > highest)
                return null;

            var step = size / (points - 1);
            var origin = center - new Vector2(half, half);
            var overview = new List<IReadOnlyList<float?>>(points);
            for (var row = 0; row < points; row++)
            {
                var line = new float?[points];
                for (var column = 0; column < points; column++)
                {
                    var height = terrain.HeightAt(origin + new Vector2(column * step, row * step));
                    line[column] = height.HasValue ? Rounding.Coarse(height.Value) : (float?)null;
                }

                overview.Add(line);
            }

            return new TerrainInfo(Rounding.Meters(step), Rounding.Coarse(lowest), Rounding.Coarse(highest), (float)Math.Round(steepest), overview);
        }

        static float Slope(float a, float b, float run) => (float)Math.Atan(Math.Abs(b - a) / run) * RadiansToDegrees;

        static string Summarize(Arguments arguments, TerrainInfo terrain, WaterInfo water, int objectCount)
        {
            var area = FormattableString.Invariant($"{arguments.Size:0.#} m square around ({arguments.X:0.#}, {arguments.Z:0.#})");
            var ground = terrain == null
                ? "no terrain (outside the world)"
                : FormattableString.Invariant($"ground {terrain.MinHeight:0.#}–{terrain.MaxHeight:0.#} m, steepest slope {terrain.SteepestSlope:0}°");
            return $"{area}: {ground}{WaterPhrase(water)}, {objectCount} object(s).";
        }

        static string WaterPhrase(WaterInfo water)
        {
            if (water == null)
                return string.Empty;
            return water.UnderwaterPercent == 0
                ? FormattableString.Invariant($", all above the sea (sea level {water.SeaLevel:0.#} m)")
                : FormattableString.Invariant($", {water.UnderwaterPercent}% under water up to {water.Deepest:0.#} m deep (sea level {water.SeaLevel:0.#} m)");
        }
    }
}
