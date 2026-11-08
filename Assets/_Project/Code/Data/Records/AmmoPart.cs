using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// What makes an item ammunition, something a weapon shoots (an arrow): what becomes of one once it lands. A part
    /// beside the <see cref="ItemPart"/>, so a bolt or a throwing knife is the same kind of data. What it does on a hit
    /// will join it.
    /// </summary>
    public sealed class AmmoPart
    {
        /// <param name="breakChance">The chance, 0 to 1, that one breaks where it lands rather than staying there to be taken back.</param>
        public AmmoPart(float breakChance)
        {
            if (!(breakChance >= 0f && breakChance <= 1f))
                throw new ArgumentOutOfRangeException(nameof(breakChance), breakChance, "A break chance is a fraction, 0 to 1.");

            BreakChance = breakChance;
        }

        /// <summary>The chance, 0 to 1, that one breaks where it lands (Skyrim's arrows sometimes do); else it can be taken back.</summary>
        public float BreakChance { get; }

        // No derived properties here: serializers write every public property, and a plugin file holds data only.
    }
}
