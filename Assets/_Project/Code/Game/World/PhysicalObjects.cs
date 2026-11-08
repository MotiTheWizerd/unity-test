using System;
using Forge.Data.Records;

namespace Forge.Game.World
{
    /// <summary>
    /// Which of the world's objects are loose physical things while it plays, and how heavy: every item, by its weight
    /// (read as Skyrim's, in pounds), and anything given a <see cref="PhysicsPart"/>, by its mass, which wins over an
    /// item's weight. The rest stand still (a house, a tree, a training dummy). Engine-free.
    /// </summary>
    public static class PhysicalObjects
    {
        /// <summary>Kilograms per unit of an item's weight: Skyrim's weights are pounds.</summary>
        public const float KilogramsPerWeight = 0.4536f;

        /// <summary>The lightest a loose thing is, kilograms: an item of no weight (gold, a feather) still has some.</summary>
        public const float LeastMass = 0.05f;

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
