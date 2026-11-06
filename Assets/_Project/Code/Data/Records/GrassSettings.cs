using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// The grass a ground layer grows (<see cref="GroundLayerRecord.Grass"/>): how many tufts stand on a square meter
    /// painted fully with the layer, and how tall they are. Where the layer is painted thinner, fewer grow. How a tuft
    /// looks is the presentation's business (its color follows the layer's look), so this is data only.
    /// </summary>
    public sealed class GrassSettings
    {
        /// <summary>Most tufts per square meter a layer may grow; more costs frame time for little gain.</summary>
        public const float MaxDensity = 8f;

        /// <summary>Tallest grass in meters.</summary>
        public const float MaxHeight = 2f;

        /// <param name="density">Tufts per square meter where the layer is painted fully, in (0, <see cref="MaxDensity"/>].</param>
        /// <param name="height">Tuft height in meters, in (0, <see cref="MaxHeight"/>]; each tuft varies around it.</param>
        public GrassSettings(float density, float height)
        {
            if (!(density > 0f) || density > MaxDensity)
                throw new ArgumentOutOfRangeException(nameof(density), density, $"Grass density must be above 0 and at most {MaxDensity} tufts per square meter.");
            if (!(height > 0f) || height > MaxHeight)
                throw new ArgumentOutOfRangeException(nameof(height), height, $"Grass height must be above 0 and at most {MaxHeight} m.");

            Density = density;
            Height = height;
        }

        public float Density { get; }

        public float Height { get; }

        // No derived properties here: serializers write every public property, and a plugin file holds data only.
    }
}
