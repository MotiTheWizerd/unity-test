using System;

namespace Forge.Editing.Terrain
{
    /// <summary>Immutable brush configuration for one stroke.</summary>
    public sealed class BrushSettings
    {
        public const float MinRadius = 1f;
        public const float MaxRadius = 64f;

        /// <summary>Height change of a full-strength Raise/Lower dab at the brush center, in meters.</summary>
        public const float MaxDabHeight = 0.5f;

        public BrushSettings(BrushMode mode, float radius, float strength)
        {
            if (!(radius >= MinRadius && radius <= MaxRadius))
                throw new ArgumentOutOfRangeException(nameof(radius), radius, $"Radius must be within [{MinRadius}, {MaxRadius}] m.");
            if (!(strength > 0f && strength <= 1f))
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be within (0, 1].");

            Mode = mode;
            Radius = radius;
            Strength = strength;
        }

        public BrushMode Mode { get; }

        public float Radius { get; }

        /// <summary>0..1 intensity of each dab.</summary>
        public float Strength { get; }

        /// <summary>Smooth falloff: 1 at the center, 0 at the rim and beyond.</summary>
        public float Weight(float distance)
        {
            if (distance >= Radius)
                return 0f;
            var t = distance / Radius;
            return 1f - t * t * (3f - 2f * t);
        }
    }
}
