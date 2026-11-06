using Forge.Editing.Placement;

namespace Forge.Editing.State
{
    /// <summary>A row gap as it is kept between sessions.</summary>
    public sealed class SavedGap
    {
        public float Gap { get; set; }
        public float RandomExtra { get; set; }

        public static SavedGap Of(RowGap gap) => new SavedGap { Gap = gap.Gap, RandomExtra = gap.RandomExtra };

        /// <summary>The gap, clamped to what a row gap can be (a number that is none counts as 0).</summary>
        public RowGap ToRowGap() => RowGap.Clamped(NumberOr0(Gap), NumberOr0(RandomExtra));

        static float NumberOr0(float value) => float.IsNaN(value) ? 0f : value;
    }
}
