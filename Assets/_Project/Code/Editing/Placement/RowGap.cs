using System;
using Forge.Data.Records;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// The space between neighbours in a row of objects, in meters: a fixed <see cref="Gap"/> plus up to
    /// <see cref="RandomExtra"/> more, drawn per gap so rows don't look regular.
    /// </summary>
    public readonly struct RowGap
    {
        /// <summary>Largest gap and largest random extra, in meters.</summary>
        public const float Max = 20f;

        public RowGap(float gap, float randomExtra)
        {
            if (!InRange(gap))
                throw new ArgumentOutOfRangeException(nameof(gap), gap, $"A gap must be within [0, {Max}] meters.");
            if (!InRange(randomExtra))
                throw new ArgumentOutOfRangeException(nameof(randomExtra), randomExtra, $"A random extra must be within [0, {Max}] meters.");

            Gap = gap;
            RandomExtra = randomExtra;
        }

        public float Gap { get; }

        public float RandomExtra { get; }

        /// <summary>Built things meet end to end (fences touch); natural things, those that vary, get some room.</summary>
        public static RowGap DefaultFor(PlacementRules rules) =>
            PlacementRoll.CanVary(rules) ? new RowGap(1f, 2f) : new RowGap(0f, 0f);

        /// <summary>A gap from free-form input (a slider), clamped into the valid range.</summary>
        public static RowGap Clamped(float gap, float randomExtra) => new RowGap(Clamp(gap), Clamp(randomExtra));

        /// <summary>One gap: <see cref="Gap"/> plus a random part of <see cref="RandomExtra"/>.</summary>
        public float Draw(Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            return Gap + (float)random.NextDouble() * RandomExtra;
        }

        static bool InRange(float value) => value >= 0f && value <= Max;

        static float Clamp(float value) => Math.Max(0f, Math.Min(Max, value));
    }
}
