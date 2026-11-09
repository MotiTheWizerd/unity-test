using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// How well a thing blocks a blow, held up against it (a shield, as Skyrim's): how much of a blocked blow it stops,
    /// and how hard that is on the one holding it. A part beside the <see cref="WeaponPart"/>, so a parrying sword can
    /// carry one too.
    /// </summary>
    public sealed class BlockPart
    {
        /// <param name="stops">How much of a blocked blow's damage it stops, a fraction, 0 to 1.</param>
        /// <param name="effort">Stamina it costs to stop each point of damage; zero or more.</param>
        public BlockPart(float stops, float effort)
        {
            if (!(stops >= 0f && stops <= 1f))
                throw new ArgumentOutOfRangeException(nameof(stops), stops, "What a block stops is a fraction, 0 to 1.");
            if (!(effort >= 0f) || float.IsInfinity(effort))
                throw new ArgumentOutOfRangeException(nameof(effort), effort, "A block's effort is a finite number, zero or more.");

            Stops = stops;
            Effort = effort;
        }

        /// <summary>How much of a blocked blow's damage it stops, 0 to 1 (an iron shield more than a round wooden one).</summary>
        public float Stops { get; }

        /// <summary>Stamina it costs the one blocking to stop each point of damage; without enough, the guard breaks.</summary>
        public float Effort { get; }

        // No derived properties here: serializers write every public property, and a plugin file holds data only.
    }
}
