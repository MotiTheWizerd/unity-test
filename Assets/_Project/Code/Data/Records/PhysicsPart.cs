using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// What makes an object that is no item a loose physical thing while the world plays (a barrel, a bucket, a stool):
    /// it falls, tumbles, is pushed and can be carried, as items are. Its <see cref="Mass"/>, in kilograms. An item needs
    /// none (it is physical by its weight); given one, the part's mass wins.
    /// </summary>
    public sealed class PhysicsPart
    {
        /// <param name="mass">Kilograms, more than zero.</param>
        public PhysicsPart(float mass)
        {
            if (!(mass > 0f) || float.IsInfinity(mass))
                throw new ArgumentOutOfRangeException(nameof(mass), mass, "A mass is a finite number of kilograms above zero.");

            Mass = mass;
        }

        /// <summary>Kilograms.</summary>
        public float Mass { get; }

        // No derived properties here: serializers write every public property, and a plugin file holds data only.
    }
}
