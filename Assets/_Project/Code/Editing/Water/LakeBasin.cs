using System;

namespace Forge.Editing.Water
{
    /// <summary>The hollow a lake can fill (<see cref="LakeFill.Survey"/>): the ground at its point and the highest level water can stand at.</summary>
    public readonly struct LakeBasin
    {
        /// <summary>How far below the rim a full lake stands, so it looks full without brimming over.</summary>
        public const float FullBelowBrim = 0.25f;

        public LakeBasin(float ground, float brim)
        {
            Ground = ground;
            Brim = brim;
        }

        /// <summary>Height of the ground at the lake's point; water must stand above it.</summary>
        public float Ground { get; }

        /// <summary>The lowest point of the hollow's rim: water up to this level stays in.</summary>
        public float Brim { get; }

        /// <summary>Whether the point is in a hollow at all, so some water can stand there.</summary>
        public bool HoldsWater => Brim > Ground;

        /// <summary>
        /// The level of a full lake, where a new one starts (the Water tool's click, add_lake without a level): just
        /// below the rim, or halfway up a hollow too shallow for that.
        /// </summary>
        public float FullLevel => Math.Max(Brim - FullBelowBrim, (Ground + Brim) / 2f);
    }
}
