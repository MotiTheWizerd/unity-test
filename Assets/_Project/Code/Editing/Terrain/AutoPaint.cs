using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Which ground layer goes where when land is painted by its shape (<see cref="AutoPaint"/>): steep ground, sloped
    /// ground, the shore just above the sea, and everything else. A role left empty goes to <see cref="Flat"/>.
    /// Angles in degrees, heights in meters; each change of ground blends over <c>Blend</c>, never a hard line.
    /// </summary>
    public sealed class AutoPaintRules
    {
        public AutoPaintRules(RecordId flat, RecordId? slope = null, RecordId? steep = null, RecordId? shore = null)
        {
            if (flat.IsDefault)
                throw new ArgumentException("The ground for flat land is required.", nameof(flat));
            Flat = flat;
            Slope = slope;
            Steep = steep;
            Shore = shore;
        }

        public RecordId Flat { get; }

        public RecordId? Slope { get; }

        public RecordId? Steep { get; }

        public RecordId? Shore { get; }

        /// <summary>Where sloped ground starts, the middle of its blend.</summary>
        public float SlopeFrom { get; set; } = 20f;

        /// <summary>Where steep ground (bare rock) starts, the middle of its blend.</summary>
        public float SteepFrom { get; set; } = 34f;

        /// <summary>The width of a slope blend.</summary>
        public float Blend { get; set; } = 8f;

        /// <summary>How far above the sea the shore reaches; ground under the sea is shore too.</summary>
        public float ShoreHeight { get; set; } = 1.5f;

        /// <summary>The width of the shore's blend into the land.</summary>
        public float ShoreBlend { get; set; } = 1f;
    }

    /// <summary>
    /// Paints land by its shape: each texel's height and slope, read from its chunk's own samples, give its ground by
    /// <see cref="AutoPaintRules"/>. Replaces the chunks' paint, heights untouched; one undo step through
    /// <see cref="Apply"/>. A new world looks natural at once, and painting by hand refines it.
    /// </summary>
    public static class AutoPaint
    {
        const int Resolution = GroundPaint.Resolution;
        const int Texels = GroundPaint.TexelCount;
        const float TexelSize = WorldGrid.ChunkSize / Resolution;

        /// <summary>
        /// Paints every chunk of <paramref name="space"/> by its shape, as one undo step named "Auto paint", with the
        /// space's sea for the shore. Returns how many chunks were painted.
        /// </summary>
        public static int Apply(EditSession session, RecordId space, AutoPaintRules rules)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var store = session.World.Records;
            var sea = store.TryGet(space, out var record) && record is SpaceRecord spaceRecord ? spaceRecord.WaterLevel : null;
            var painted = Paint(store, space, rules, sea);
            if (painted.Count == 0)
                return 0;

            session.Batch("Auto paint", () =>
            {
                foreach (var chunk in painted)
                    session.Put(chunk);
            });
            return painted.Count;
        }

        /// <summary>Every chunk of <paramref name="space"/> repainted by its shape; <paramref name="seaLevel"/> null for no shore.</summary>
        public static IReadOnlyList<TerrainChunkRecord> Paint(IRecordStore store, RecordId space, AutoPaintRules rules, float? seaLevel)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            // Each role's layer, in a fixed order; an empty role falls to flat ground.
            var roles = new[] { rules.Flat, rules.Slope ?? rules.Flat, rules.Steep ?? rules.Flat, rules.Shore ?? rules.Flat };
            var layers = roles.Distinct().ToList();
            var slot = roles.Select(role => layers.IndexOf(role)).ToArray();

            var grid = new TerrainGrid(store, space);
            return store.OfType<TerrainChunkRecord>()
                .Where(chunk => chunk.Space == space && grid.TryGetChunk(chunk.Coord, out var winner) && winner.Id == chunk.Id)
                .Select(chunk => chunk.WithPaint(PaintOf(chunk.Heights.Load(), rules, seaLevel, layers, slot)))
                .ToList();
        }

        static GroundPaint PaintOf(HeightField heights, AutoPaintRules rules, float? seaLevel, List<RecordId> layers, int[] slot)
        {
            var n = heights.Resolution;
            var spacing = WorldGrid.ChunkSize / (n - 1);
            var weights = new float[layers.Count * Texels];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
            {
                // The texel's center in sample units, and the cell of samples around it.
                var u = (x + 0.5f) * TexelSize / spacing;
                var v = (z + 0.5f) * TexelSize / spacing;
                var i = Math.Min((int)u, n - 2);
                var j = Math.Min((int)v, n - 2);
                var fu = u - i;
                var fv = v - j;
                float h00 = heights[i, j], h10 = heights[i + 1, j], h01 = heights[i, j + 1], h11 = heights[i + 1, j + 1];

                var height = Lerp(Lerp(h00, h10, fu), Lerp(h01, h11, fu), fv);
                var dx = Lerp(h10 - h00, h11 - h01, fv) / spacing;
                var dz = Lerp(h01 - h00, h11 - h10, fu) / spacing;
                var slope = (float)(Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);

                var steep = Ramp(slope, rules.SteepFrom, rules.Blend);
                var rest = 1f - steep;
                var shore = seaLevel.HasValue ? rest * (1f - Ramp(height, seaLevel.Value + rules.ShoreHeight, rules.ShoreBlend)) : 0f;
                var sloped = (rest - shore) * Ramp(slope, rules.SlopeFrom, rules.Blend);
                var flat = rest - shore - sloped;

                var texel = z * Resolution + x;
                weights[slot[0] * Texels + texel] += flat;
                weights[slot[1] * Texels + texel] += sloped;
                weights[slot[2] * Texels + texel] += steep;
                weights[slot[3] * Texels + texel] += shore;
            }

            return GroundPaint.FromWeights(layers, weights);
        }

        // 0 below the blend, 1 above it, smooth across its width around `middle`.
        static float Ramp(float value, float middle, float width)
        {
            var t = (value - (middle - width / 2f)) / width;
            if (t <= 0f)
                return 0f;
            if (t >= 1f)
                return 1f;
            return t * t * (3f - 2f * t);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
