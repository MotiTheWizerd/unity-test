using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Editing.Geometry;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Evens out the ground over an area: each sample moves toward the average of the ground around it, taken over about
    /// <c>size</c> meters, so bumps, terraces and sharp banks soften while the land's larger shape stays. Full inside the
    /// area, easing out over the blend around it. The average is a box blur run three times (close to a Gaussian), over
    /// the land only, so the land's edge doesn't pull the ground down.
    /// </summary>
    public sealed class GroundSmoothing : TerrainEdit
    {
        /// <summary>The largest stretch averaged, in meters.</summary>
        public const float MaxSize = 32f;

        /// <summary>The widest soft edge around the area, in meters.</summary>
        public const float MaxBlend = AreaFlattening.MaxBlend;

        /// <summary>The most an area may span, east-west or north-south, in meters.</summary>
        public const float MaxSpan = 256f;

        const int Passes = 3;

        // Moves smaller than this (a tenth of a millimetre: rounding, on ground that is already even) aren't written.
        const float Unchanged = 1e-4f;

        /// <param name="store">World to edit; the terrain of <paramref name="space"/> is snapshotted now.</param>
        /// <param name="space">The worldspace whose land is smoothed.</param>
        /// <param name="area">The ground to smooth fully.</param>
        /// <param name="blend">Width of the soft edge around the area, in meters; 0 for a hard edge.</param>
        /// <param name="size">How wide a stretch each sample is averaged over, in meters: the size of the bumps that go.</param>
        /// <param name="strength">How far the ground moves toward the average: 1 all the way.</param>
        public GroundSmoothing(IRecordStore store, RecordId space, GroundShape area, float blend, float size, float strength)
            : base(store, space)
        {
            Area = area ?? throw new ArgumentNullException(nameof(area));
            if (!(blend >= 0f && blend <= MaxBlend))
                throw new ArgumentOutOfRangeException(nameof(blend), blend, $"Blend must be within [0, {MaxBlend}] m.");
            if (!(size > 0f && size <= MaxSize))
                throw new ArgumentOutOfRangeException(nameof(size), size, $"Size must be within (0, {MaxSize}] m.");
            if (!(strength > 0f && strength <= 1f))
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be within (0, 1].");
            if (area.Span.X > MaxSpan || area.Span.Y > MaxSpan)
                throw new ArgumentException(FormattableString.Invariant(
                    $"That area spans {area.Span.X:0} × {area.Span.Y:0} m; smooth at most {MaxSpan:0} m across at a time."));

            Blend = blend;
            Size = size;

            // The samples touched, and around them a margin the blur reads from: each pass reaches `radius` samples.
            var spacing = Terrain.Spacing;
            var radius = Math.Max(1, (int)Math.Round(size / 2f / spacing));
            var margin = radius * Passes;
            var firstX = (int)Math.Ceiling((area.Min.X - blend) / spacing) - margin;
            var firstZ = (int)Math.Ceiling((area.Min.Y - blend) / spacing) - margin;
            var columns = (int)Math.Floor((area.Max.X + blend) / spacing) + margin - firstX + 1;
            var rows = (int)Math.Floor((area.Max.Y + blend) / spacing) + margin - firstZ + 1;

            var heights = new float[columns * rows];
            for (var z = 0; z < rows; z++)
            for (var x = 0; x < columns; x++)
                heights[z * columns + x] = GlobalHeight(firstX + x, firstZ + z) ?? float.NaN;

            var average = (float[])heights.Clone();
            for (var pass = 0; pass < Passes; pass++)
            {
                average = BoxBlur(average, columns, rows, radius, along: 1, lines: rows, lineStep: columns, length: columns);
                average = BoxBlur(average, columns, rows, radius, along: columns, lines: columns, lineStep: 1, length: rows);
            }

            var updates = new List<(int gx, int gz, float height)>();
            for (var z = margin; z < rows - margin; z++)
            for (var x = margin; x < columns - margin; x++)
            {
                var index = z * columns + x;
                var current = heights[index];
                if (float.IsNaN(current))
                    continue;

                var gx = firstX + x;
                var gz = firstZ + z;
                var outside = area.SignedDistance(new Vector2(gx * spacing, gz * spacing));
                var weight = outside <= 0f ? 1f : blend > 0f ? Falloff.Smooth(outside / blend) : 0f;
                if (weight <= 0f)
                    continue;

                Samples++;
                var next = current + (average[index] - current) * weight * strength;
                if (Math.Abs(next - current) < Unchanged)
                    continue;

                MostRaised = Math.Max(MostRaised, next - current);
                MostLowered = Math.Max(MostLowered, current - next);
                updates.Add((gx, gz, next));
            }

            Write(updates);
        }

        public GroundShape Area { get; }

        public float Blend { get; }

        public float Size { get; }

        /// <summary>How many samples of land the area and its blend cover; 0 when there is no land under it.</summary>
        public int Samples { get; }

        /// <summary>The most any ground was raised, in meters.</summary>
        public float MostRaised { get; }

        /// <summary>The most any ground was lowered, in meters.</summary>
        public float MostLowered { get; }

        public override string Description => "Smooth ground";

        // One pass of a box blur along one axis: each sample becomes the mean of the land samples within `radius` of it
        // on its line (rows, `along` 1, or columns, `along` the row length); missing ground stays missing.
        static float[] BoxBlur(float[] source, int columns, int rows, int radius, int along, int lines, int lineStep, int length)
        {
            var result = new float[columns * rows];
            for (var line = 0; line < lines; line++)
            {
                var start = line * lineStep;
                var sum = 0.0;
                var count = 0;

                // The window starts as [-radius, radius] around the line's first sample.
                for (var i = 0; i <= Math.Min(radius, length - 1); i++)
                    Add(source[start + i * along], ref sum, ref count, 1);

                for (var i = 0; i < length; i++)
                {
                    var value = source[start + i * along];
                    result[start + i * along] = float.IsNaN(value) || count == 0 ? value : (float)(sum / count);

                    var enter = i + radius + 1;
                    var leave = i - radius;
                    if (enter < length)
                        Add(source[start + enter * along], ref sum, ref count, 1);
                    if (leave >= 0)
                        Add(source[start + leave * along], ref sum, ref count, -1);
                }
            }

            return result;
        }

        static void Add(float value, ref double sum, ref int count, int sign)
        {
            if (float.IsNaN(value))
                return;
            sum += sign * value;
            count += sign;
        }
    }
}
