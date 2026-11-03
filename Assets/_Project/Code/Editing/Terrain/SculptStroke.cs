using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data.Plugins;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>One brush stroke: dabs of one brush into a working copy of the terrain.</summary>
    public sealed class SculptStroke : TerrainEdit
    {
        /// <param name="store">World to sculpt; its terrain is snapshotted now.</param>
        /// <param name="brush">Brush used for every dab of this stroke.</param>
        /// <param name="start">World XZ where the stroke began (sets the Flatten target).</param>
        public SculptStroke(IRecordStore store, BrushSettings brush, Vector2 start)
            : base(store)
        {
            Brush = brush ?? throw new ArgumentNullException(nameof(brush));
            FlattenHeight = SampleHeight(start) ?? 0f;
        }

        public BrushSettings Brush { get; }

        public float FlattenHeight { get; }

        public override string Description => $"Sculpt ({Brush.Mode})";

        /// <summary>Applies one dab centered at world XZ. Returns the chunks it changed.</summary>
        public IReadOnlyCollection<ChunkCoord> ApplyDab(Vector2 center)
        {
            var radius = Brush.Radius;
            var spacing = Terrain.Spacing;
            var minX = (int)Math.Floor((center.X - radius) / spacing);
            var maxX = (int)Math.Ceiling((center.X + radius) / spacing);
            var minZ = (int)Math.Floor((center.Y - radius) / spacing);
            var maxZ = (int)Math.Ceiling((center.Y + radius) / spacing);

            var updates = new List<(int gx, int gz, float height)>();
            for (var gz = minZ; gz <= maxZ; gz++)
            for (var gx = minX; gx <= maxX; gx++)
            {
                var current = GlobalHeight(gx, gz);
                if (current == null)
                    continue;

                var distance = Vector2.Distance(center, new Vector2(gx * spacing, gz * spacing));
                var weight = Brush.Weight(distance) * Brush.Strength;
                if (weight > 0f)
                    updates.Add((gx, gz, Apply(current.Value, weight, gx, gz)));
            }

            return Write(updates);
        }

        float Apply(float current, float weight, int gx, int gz)
        {
            switch (Brush.Mode)
            {
                case BrushMode.Raise:
                    return current + BrushSettings.MaxDabHeight * weight;
                case BrushMode.Lower:
                    return current - BrushSettings.MaxDabHeight * weight;
                case BrushMode.Flatten:
                    return current + (FlattenHeight - current) * weight;
                case BrushMode.Smooth:
                    var average = NeighbourAverage(gx, gz) ?? current;
                    return current + (average - current) * weight;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Brush.Mode), Brush.Mode, null);
            }
        }

        float? NeighbourAverage(int gx, int gz)
        {
            var sum = 0f;
            var count = 0;
            for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var h = GlobalHeight(gx + dx, gz + dz);
                if (h == null)
                    continue;
                sum += h.Value;
                count++;
            }

            return count > 0 ? sum / count : (float?)null;
        }
    }
}
