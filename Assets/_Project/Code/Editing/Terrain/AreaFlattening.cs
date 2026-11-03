using System;
using System.Collections.Generic;
using Forge.Data.Plugins;
using Forge.Data.Terrain;
using Forge.Editing.Geometry;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Levels a rectangle of ground to one height, e.g. a building site, and eases the ground around it back to
    /// the natural terrain over a soft edge, so the site doesn't end in a cliff or a step.
    /// </summary>
    public sealed class AreaFlattening : TerrainEdit
    {
        /// <summary>Widest soft edge around the area, in meters.</summary>
        public const float MaxBlend = 32f;

        /// <param name="store">World to edit; its terrain is snapshotted now.</param>
        /// <param name="area">The ground to make level.</param>
        /// <param name="height">The height to level it to, in meters.</param>
        /// <param name="blend">Width of the soft edge around the area, in meters; 0 for a hard edge.</param>
        public AreaFlattening(IRecordStore store, GroundRect area, float height, float blend)
            : base(store)
        {
            if (!(height >= 0f && height <= WorldGrid.MaxTerrainHeight))
                throw new ArgumentOutOfRangeException(nameof(height), height, $"Height must be within [0, {WorldGrid.MaxTerrainHeight}] m.");
            if (!(blend >= 0f && blend <= MaxBlend))
                throw new ArgumentOutOfRangeException(nameof(blend), blend, $"Blend must be within [0, {MaxBlend}] m.");

            Area = area;
            Height = height;
            Blend = blend;

            var updates = new List<(int gx, int gz, float height)>();
            foreach (var sample in Terrain.SamplesAround(area.Center, area.Reach + blend))
            {
                var weight = Weight(area.DistanceOutside(sample.Ground));
                if (weight <= 0f)
                    continue;

                var next = sample.Height + (height - sample.Height) * weight;
                MostRaised = Math.Max(MostRaised, next - sample.Height);
                MostLowered = Math.Max(MostLowered, sample.Height - next);
                updates.Add((sample.Gx, sample.Gz, next));
            }

            Write(updates);
        }

        public GroundRect Area { get; }

        public float Height { get; }

        public float Blend { get; }

        /// <summary>The most any ground was raised, in meters.</summary>
        public float MostRaised { get; }

        /// <summary>The most any ground was lowered, in meters.</summary>
        public float MostLowered { get; }

        public override string Description => "Flatten area";

        // Fully level inside the area, easing out to untouched ground at the edge of the blend.
        float Weight(float outside)
        {
            if (outside <= 0f)
                return 1f;
            return Blend > 0f ? Falloff.Smooth(outside / Blend) : 0f;
        }
    }
}
