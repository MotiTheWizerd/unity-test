using System;

namespace Forge.Editing.Terrain
{
    /// <summary>The footprint of a terrain brush, sculpt or paint alike: its radius, strength and falloff. Immutable.</summary>
    public sealed class BrushShape
    {
        public const float MinRadius = 1f;
        public const float MaxRadius = 64f;

        public BrushShape(float radius, float strength)
        {
            if (!(radius >= MinRadius && radius <= MaxRadius))
                throw new ArgumentOutOfRangeException(nameof(radius), radius, $"Radius must be within [{MinRadius}, {MaxRadius}] m.");
            if (!(strength > 0f && strength <= 1f))
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be within (0, 1].");

            Radius = radius;
            Strength = strength;
        }

        public float Radius { get; }

        /// <summary>0..1 intensity of each dab.</summary>
        public float Strength { get; }

        /// <summary>Smooth falloff: 1 at the center, 0 at the rim and beyond.</summary>
        public float Weight(float distance) => Falloff.Smooth(distance / Radius);
    }
}
