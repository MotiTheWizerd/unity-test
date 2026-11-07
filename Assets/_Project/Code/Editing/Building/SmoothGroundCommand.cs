using System;
using System.Collections.Generic;
using Forge.Editing.Commands;
using Forge.Editing.Terrain;

namespace Forge.Editing.Building
{
    /// <summary>Evens out the ground over an area: the sculpt brush's smooth as one shape.</summary>
    public sealed class SmoothGroundCommand : EditorCommand<SmoothGroundCommand.Arguments, SmoothGroundCommand.Result>
    {
        public const float DefaultSize = 6f;
        public const float DefaultBlend = 4f;

        readonly BuildContext _context;

        public SmoothGroundCommand(BuildContext context)
            : base("smooth_ground",
                "Smooths the ground over an area: bumps, the steps left between levelled or sculpted shapes and sharp " +
                "banks soften, while hills and slopes keep their larger shape. Give either a rectangle (x, z, width, " +
                "depth and heading, the way flatten_area takes one) or points: the corners of any shape, in order around " +
                "it. size is how wide a stretch is averaged, about the size of the bumps that go; smooth again, or with a " +
                "larger size, to smooth more. The ground eases back into the land around it over the blend. Objects on " +
                "the reshaped ground move with it.")
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public sealed class Arguments
        {
            [Param("Rectangle: east-west center, in meters.", Optional = true)]
            public float? X { get; set; }

            [Param("Rectangle: north-south center, in meters.", Optional = true)]
            public float? Z { get; set; }

            [Param("Rectangle: size across its front, in meters.", Optional = true, Minimum = 1, Maximum = GroundSmoothing.MaxSpan)]
            public float? Width { get; set; }

            [Param("Rectangle: size from its front to back, in meters.", Optional = true, Minimum = 1, Maximum = GroundSmoothing.MaxSpan)]
            public float? Depth { get; set; }

            [Param("Rectangle: compass direction its front faces, in degrees (0 north, 90 east). Default 0.", Optional = true)]
            public float? Heading { get; set; }

            [Param("Any other shape: its corners, in meters, in order around it. Instead of the rectangle.", Optional = true, MinItems = 3, MaxItems = 64)]
            public List<GroundPoint> Points { get; set; }

            [Param("How wide a stretch each spot is averaged over, in meters: about the size of the bumps that go. Default 6.",
                Optional = true, Minimum = 1, Maximum = GroundSmoothing.MaxSize)]
            public float? Size { get; set; }

            [Param("How far the ground moves toward smooth: 1 all the way (default), less to soften it a little.",
                Optional = true, Minimum = 0.05, Maximum = 1)]
            public float? Strength { get; set; }

            [Param("Width of the soft edge around the area, in meters; 0 for a hard edge. Default 4.",
                Optional = true, Minimum = 0, Maximum = GroundSmoothing.MaxBlend)]
            public float? Blend { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, float mostRaised, float mostLowered, int movedObjects)
            {
                Summary = summary;
                MostRaised = mostRaised;
                MostLowered = mostLowered;
                MovedObjects = movedObjects;
            }

            public string Summary { get; }

            /// <summary>The most any ground was raised, in meters.</summary>
            public float MostRaised { get; }

            /// <summary>The most any ground was lowered, in meters.</summary>
            public float MostLowered { get; }

            /// <summary>How many objects moved with the ground.</summary>
            public int MovedObjects { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            var (area, words) = GroundArea.Of(arguments.X, arguments.Z, arguments.Width, arguments.Depth, arguments.Heading, arguments.Points);
            var size = arguments.Size ?? DefaultSize;
            var smoothing = Refusal.Of(() => new GroundSmoothing(_context.Session.World.Records, _context.Space, area,
                arguments.Blend ?? DefaultBlend, size, arguments.Strength ?? 1f));
            if (smoothing.Samples == 0)
                throw new CommandException("There is no land under that area; inspect_area shows where the ground is.");
            TerrainEdits.Commit(_context.Session, smoothing, _context.FollowTerrain(), out var moved);

            var summary = FormattableString.Invariant($"Smoothed {words} over {size:0.#} m stretches");
            if (smoothing.TouchedChunks.Count == 0)
                summary += ": the ground was already smooth there.";
            else
                summary += FormattableString.Invariant(
                    $": raised the ground by up to {smoothing.MostRaised:0.##} m and lowered it by up to {smoothing.MostLowered:0.##} m.");
            if (moved > 0)
                summary += $" {moved} object(s) moved with the ground.";

            return new Result(summary, Rounding.Meters(smoothing.MostRaised), Rounding.Meters(smoothing.MostLowered), moved);
        }
    }
}
