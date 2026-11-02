using System;
using Forge.Data.Records;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// One random draw from a <see cref="PlacementVariation"/>: the extra turn and the scale the
    /// next placed instance gets. Drawn ahead of the click, so the preview shows the exact result.
    /// </summary>
    public readonly struct PlacementRoll
    {
        public PlacementRoll(float yawDegrees, float scale)
        {
            YawDegrees = yawDegrees;
            Scale = scale;
        }

        /// <summary>No variation: no extra turn, scale 1.</summary>
        public static PlacementRoll Identity => new PlacementRoll(0f, 1f);

        /// <summary>Extra turn around the vertical axis, in degrees [0, 360).</summary>
        public float YawDegrees { get; }

        public float Scale { get; }

        /// <summary>Draws from <paramref name="variation"/>; null means <see cref="Identity"/>.</summary>
        public static PlacementRoll Draw(PlacementVariation variation, Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (variation == null)
                return Identity;

            // Whole degrees and hundredths of scale: plenty of variety, and plugin files stay readable.
            var yaw = variation.RandomYaw ? random.Next(360) : 0f;
            var scale = variation.MinScale + (float)random.NextDouble() * (variation.MaxScale - variation.MinScale);
            scale = (float)Math.Round(scale, 2);
            return new PlacementRoll(yaw, Math.Max(variation.MinScale, Math.Min(variation.MaxScale, scale)));
        }
    }
}
