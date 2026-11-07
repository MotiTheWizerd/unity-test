using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Terrain
{
    /// <summary>
    /// Which ground layer goes where when land is painted by its shape (<see cref="AutoPaint"/>): snow above the snow
    /// line, steep ground, sloped ground, the shore just above the sea, and everything else. A role left empty goes to
    /// <see cref="Flat"/>. Angles in degrees, heights in meters; each change of ground blends, never a hard line.
    /// </summary>
    public sealed class AutoPaintRules
    {
        public AutoPaintRules(RecordId flat, RecordId? slope = null, RecordId? steep = null, RecordId? shore = null, RecordId? snow = null)
        {
            if (flat.IsDefault)
                throw new ArgumentException("The ground for flat land is required.", nameof(flat));
            Flat = flat;
            Slope = slope;
            Steep = steep;
            Shore = shore;
            Snow = snow;
        }

        public RecordId Flat { get; }

        public RecordId? Slope { get; }

        public RecordId? Steep { get; }

        public RecordId? Shore { get; }

        public RecordId? Snow { get; }

        /// <summary>Where sloped ground starts, the middle of its blend.</summary>
        public float SlopeFrom { get; set; } = 20f;

        /// <summary>Where steep ground (bare rock) starts, the middle of its blend.</summary>
        public float SteepFrom { get; set; } = 34f;

        /// <summary>The width of a slope blend.</summary>
        public float Blend { get; set; } = 8f;

        /// <summary>
        /// How far, in degrees, the slope thresholds wander over the ground, so an even slope breaks into patches of
        /// dirt and rock instead of clean bands around a hill; 0 for exact thresholds.
        /// </summary>
        public float SlopeWander { get; set; } = 6f;

        /// <summary>How far above the sea the shore reaches; ground under the sea is shore too.</summary>
        public float ShoreHeight { get; set; } = 1.5f;

        /// <summary>The width of the shore's blend into the land.</summary>
        public float ShoreBlend { get; set; } = 1f;

        /// <summary>
        /// The height snow lies from, the middle of its blend; null for no snow. The line wanders by up to
        /// <see cref="SnowBlend"/> with the ground, as real snow lines do.
        /// </summary>
        public float? SnowLine { get; set; }

        /// <summary>The height over which the ground turns to snow.</summary>
        public float SnowBlend { get; set; } = 12f;

        /// <summary>Where snow no longer holds and the ground stays bare (steep ground's), the middle of its blend.</summary>
        public float SnowHoldsTo { get; set; } = 52f;

        /// <summary>
        /// The base game's rules, its roles found by their ground looks: dirt on slopes, rock on steep ground, sand at
        /// the shore, snow above a snow line, and <paramref name="flat"/> elsewhere. A look the world lacks leaves its
        /// role empty.
        /// </summary>
        public static AutoPaintRules OfBaseLooks(IRecordStore store, RecordId flat, float? snowLine = null)
        {
            // SHORTCUT: the roles are the base game's looks; per-world rules (a climate's or worldspace's) become data
            // when worlds need their own ground (see ARCHITECTURE.md).
            RecordId? Look(string assetKey) => GroundLayers.WithLook(store, assetKey);
            return new AutoPaintRules(flat, Look("ground/dirt"), Look("ground/rock"), Look("ground/sand"), Look("ground/snow"))
            {
                SnowLine = snowLine,
            };
        }
    }

    /// <summary>
    /// Paints land by its shape: each texel's height and slope, read from its chunk's own samples, give its ground by
    /// <see cref="AutoPaintRules"/>. Replaces the chunks' paint, heights untouched; one undo step through
    /// <see cref="Apply"/>. A new world looks natural at once, and painting by hand refines it. <see cref="AutoPainting"/>
    /// paints an area the same way.
    /// </summary>
    public static class AutoPaint
    {
        /// <summary>How many roles there are, in the order <see cref="Roles"/> and <see cref="Shares"/> give them.</summary>
        internal const int RoleCount = 5;

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
            var painted = Paint(store, space, rules, SeaLevelOf(store, space));
            if (painted.Count == 0)
                return 0;

            session.Batch("Auto paint", () =>
            {
                foreach (var chunk in painted)
                    session.Put(chunk);
            });
            return painted.Count;
        }

        /// <summary>The sea level of <paramref name="space"/>, or null when it has no sea.</summary>
        public static float? SeaLevelOf(IRecordStore store, RecordId space) =>
            store.TryGet(space, out var record) && record is SpaceRecord spaceRecord ? spaceRecord.WaterLevel : null;

        /// <summary>Every chunk of <paramref name="space"/> repainted by its shape; <paramref name="seaLevel"/> null for no shore.</summary>
        public static IReadOnlyList<TerrainChunkRecord> Paint(IRecordStore store, RecordId space, AutoPaintRules rules, float? seaLevel)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            var roles = Roles(rules);
            var layers = roles.Distinct().ToList();
            var slot = roles.Select(role => layers.IndexOf(role)).ToArray();

            var grid = new TerrainGrid(store, space);
            return store.OfType<TerrainChunkRecord>()
                .Where(chunk => chunk.Space == space && grid.TryGetChunk(chunk.Coord, out var winner) && winner.Id == chunk.Id)
                .Select(chunk => chunk.WithPaint(PaintOf(chunk.Heights.Load(), chunk.Coord, rules, seaLevel, layers, slot)))
                .ToList();
        }

        /// <summary>Each role's ground, in a fixed order (flat, slope, steep, shore, snow); an empty role falls to flat ground.</summary>
        internal static RecordId[] Roles(AutoPaintRules rules) =>
            new[] { rules.Flat, rules.Slope ?? rules.Flat, rules.Steep ?? rules.Flat, rules.Shore ?? rules.Flat, rules.Snow ?? rules.Flat };

        /// <summary>
        /// Writes into <paramref name="shares"/> each role's share (in <see cref="Roles"/>' order, adding up to 1) of
        /// texel (<paramref name="x"/>, <paramref name="z"/>) of the chunk at <paramref name="coord"/>, by the height and
        /// slope its <paramref name="heights"/> give there.
        /// </summary>
        internal static void Shares(HeightField heights, ChunkCoord coord, int x, int z, AutoPaintRules rules, float? seaLevel, float[] shares)
        {
            var n = heights.Resolution;
            var spacing = WorldGrid.ChunkSize / (n - 1);

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

            var ground = new Vector2(coord.X * WorldGrid.ChunkSize + (x + 0.5f) * TexelSize, coord.Z * WorldGrid.ChunkSize + (z + 0.5f) * TexelSize);
            var snow = 0f;
            if (rules.SnowLine is float snowLine)
            {
                var wander = TerrainNoise.At(ground, 24f, 3, 3) * rules.SnowBlend;
                snow = Ramp(height + wander, snowLine, rules.SnowBlend) * (1f - Ramp(slope, rules.SnowHoldsTo, rules.Blend));
            }

            // The slope as the thresholds read it, wandering over the ground so even slopes paint in patches.
            var patchy = rules.SlopeWander > 0f ? slope + TerrainNoise.At(ground, 16f, 2, 5) * rules.SlopeWander : slope;
            var bare = 1f - snow;
            var steep = bare * Ramp(patchy, rules.SteepFrom, rules.Blend);
            var rest = bare - steep;
            var shore = seaLevel.HasValue ? rest * (1f - Ramp(height, seaLevel.Value + rules.ShoreHeight, rules.ShoreBlend)) : 0f;
            var sloped = (rest - shore) * Ramp(patchy, rules.SlopeFrom, rules.Blend);

            // Each share is what the ones before it left, so round-off can take one a hair below zero.
            shares[0] = Math.Max(0f, rest - shore - sloped);
            shares[1] = Math.Max(0f, sloped);
            shares[2] = Math.Max(0f, steep);
            shares[3] = Math.Max(0f, shore);
            shares[4] = Math.Max(0f, snow);
        }

        static GroundPaint PaintOf(HeightField heights, ChunkCoord coord, AutoPaintRules rules, float? seaLevel, List<RecordId> layers, int[] slot)
        {
            var weights = new float[layers.Count * Texels];
            var shares = new float[RoleCount];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
            {
                Shares(heights, coord, x, z, rules, seaLevel, shares);
                var texel = z * Resolution + x;
                for (var role = 0; role < RoleCount; role++)
                    weights[slot[role] * Texels + texel] += shares[role];
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
