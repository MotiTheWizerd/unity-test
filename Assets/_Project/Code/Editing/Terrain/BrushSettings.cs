using System;

namespace Forge.Editing.Terrain
{
    /// <summary>Immutable sculpt brush configuration for one stroke: a mode and a <see cref="BrushShape"/>.</summary>
    public sealed class BrushSettings
    {
        public const float MinRadius = BrushShape.MinRadius;
        public const float MaxRadius = BrushShape.MaxRadius;

        /// <summary>Height change of a full-strength Raise/Lower dab at the brush center, in meters.</summary>
        public const float MaxDabHeight = 0.5f;

        public BrushSettings(BrushMode mode, float radius, float strength)
        {
            Mode = mode;
            Shape = new BrushShape(radius, strength);
        }

        public BrushMode Mode { get; }

        public BrushShape Shape { get; }

        public float Radius => Shape.Radius;

        /// <summary>0..1 intensity of each dab.</summary>
        public float Strength => Shape.Strength;

        /// <summary>Smooth falloff: 1 at the center, 0 at the rim and beyond.</summary>
        public float Weight(float distance) => Shape.Weight(distance);
    }
}
