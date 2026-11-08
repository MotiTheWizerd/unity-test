using System;
using Forge.Data.Records;

namespace Forge.Game.World
{
    /// <summary>
    /// Which of the world's objects are loose physical things while it plays, and how heavy: every item, by its weight
    /// (read as Skyrim's, in pounds), and anything given a <see cref="PhysicsPart"/>, by its mass, which wins over an
    /// item's weight. The rest stand still (a house, a tree, a training dummy). The light ones are clutter
    /// (<see cref="IsClutter"/>), which actors walk through, kicking it aside. Engine-free.
    /// </summary>
    public static class PhysicalObjects
    {
        /// <summary>Kilograms per unit of an item's weight: Skyrim's weights are pounds.</summary>
        public const float KilogramsPerWeight = 0.4536f;

        /// <summary>The lightest a loose thing is, kilograms: an item of no weight (gold, a feather) still has some.</summary>
        public const float LeastMass = 0.05f;

        /// <summary>
        /// The heaviest a loose thing is that is clutter, kilograms: a cup, a sword, a basket. Heavier ones (a full barrel)
        /// stand in an actor's way as the rest of the world does.
        /// </summary>
        public const float MostClutterMass = 10f;

        /// <summary>
        /// Whether a loose thing of <paramref name="mass"/> kilograms is clutter: never in an actor's way (it can't be
        /// stood on or walked into), kicked aside by its feet instead (<see cref="ClutterKick"/>).
        /// </summary>
        public static bool IsClutter(float mass) => mass <= MostClutterMass;

        /// <summary>Whether <paramref name="baseObject"/> is a loose physical thing, and its mass in kilograms.</summary>
        public static bool TryGetMass(BaseObjectRecord baseObject, out float mass)
        {
            if (baseObject == null)
                throw new ArgumentNullException(nameof(baseObject));
            if (baseObject.Physics != null)
            {
                mass = baseObject.Physics.Mass;
                return true;
            }

            if (baseObject.Item != null)
            {
                mass = Math.Max(LeastMass, baseObject.Item.Weight * KilogramsPerWeight);
                return true;
            }

            mass = 0f;
            return false;
        }
    }
}
