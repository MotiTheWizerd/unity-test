using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// How much new instances of a base object may vary when placed, so a forest of the same
    /// tree doesn't look stamped: an optional random turn around the vertical axis and a
    /// uniform scale picked from [<see cref="MinScale"/>, <see cref="MaxScale"/>].
    /// Authoring data only: placed instances store their own final transform.
    /// </summary>
    public sealed class PlacementVariation
    {
        public PlacementVariation(bool randomYaw, float minScale, float maxScale)
        {
            if (!(minScale > 0f) || float.IsInfinity(minScale))
                throw new ArgumentOutOfRangeException(nameof(minScale), minScale, "Minimum scale must be a positive, finite number.");
            if (!(maxScale >= minScale) || float.IsInfinity(maxScale))
                throw new ArgumentOutOfRangeException(nameof(maxScale), maxScale, "Maximum scale must be finite and at least the minimum scale.");

            RandomYaw = randomYaw;
            MinScale = minScale;
            MaxScale = maxScale;
        }

        /// <summary>Turn each new instance to a random heading.</summary>
        public bool RandomYaw { get; }

        public float MinScale { get; }

        public float MaxScale { get; }
    }
}
