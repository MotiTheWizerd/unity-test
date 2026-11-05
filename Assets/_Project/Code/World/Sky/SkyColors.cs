using UnityEngine;

namespace Forge.World.Sky
{
    /// <summary>The sky's colors for one height of the sun, from <see cref="SkyProfile.ColorsAt"/>.</summary>
    public readonly struct SkyColors
    {
        public SkyColors(Color zenith, Color horizon, Color sunLight, float sunIntensity)
        {
            Zenith = zenith;
            Horizon = horizon;
            SunLight = sunLight;
            SunIntensity = sunIntensity;
        }

        /// <summary>The sky straight up.</summary>
        public Color Zenith { get; }

        /// <summary>The sky at the horizon and below it, which is also the fog's color.</summary>
        public Color Horizon { get; }

        /// <summary>The sun's light and disk.</summary>
        public Color SunLight { get; }

        public float SunIntensity { get; }
    }
}
