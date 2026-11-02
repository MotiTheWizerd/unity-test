using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// How the editor places new instances of a base object: an optional random turn around the
    /// vertical axis and a uniform scale picked from [<see cref="MinScale"/>, <see cref="MaxScale"/>],
    /// so a forest of the same tree doesn't look stamped; and whether to tilt the object to the
    /// slope of the ground under it (fences, walls, rocks) or keep it upright (trees, houses).
    /// Authoring data only: placed instances store their own final transform.
    /// </summary>
    public sealed class PlacementRules
    {
        public PlacementRules(bool randomYaw, float minScale, float maxScale, bool alignToSlope)
        {
            if (!(minScale > 0f) || float.IsInfinity(minScale))
                throw new ArgumentOutOfRangeException(nameof(minScale), minScale, "Minimum scale must be a positive, finite number.");
            if (!(maxScale >= minScale) || float.IsInfinity(maxScale))
                throw new ArgumentOutOfRangeException(nameof(maxScale), maxScale, "Maximum scale must be finite and at least the minimum scale.");

            RandomYaw = randomYaw;
            MinScale = minScale;
            MaxScale = maxScale;
            AlignToSlope = alignToSlope;
        }

        /// <summary>Turn each new instance to a random heading.</summary>
        public bool RandomYaw { get; }

        public float MinScale { get; }

        public float MaxScale { get; }

        /// <summary>Tilt each new instance to the ground's slope under its footprint instead of keeping it upright.</summary>
        public bool AlignToSlope { get; }

        // No derived properties here: serializers write every public property, and a plugin file holds data only.
    }
}
