using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Data;
using Forge.Editing.Geometry;
using Forge.Editing.Terrain;
using Forge.Editing.Water;

namespace Forge.Editing.Placement
{
    /// <summary>Where a scatter may put things: the ground's slope and height, dry or not, how far apart, how clustered.</summary>
    public sealed class ScatterRules
    {
        /// <summary>Gentlest ground, in degrees.</summary>
        public float MinSlope { get; set; }

        /// <summary>Steepest ground, in degrees.</summary>
        public float MaxSlope { get; set; } = 90f;

        /// <summary>Lowest ground, in meters; null for no limit.</summary>
        public float? MinHeight { get; set; }

        /// <summary>Highest ground, in meters; null for no limit.</summary>
        public float? MaxHeight { get; set; }

        /// <summary>The least distance between two spots, and from what already stands there, in meters.</summary>
        public float Spacing { get; set; } = 3f;

        /// <summary>0 spreads evenly; toward 1 the spots gather in clusters with open ground between.</summary>
        public float Clustering { get; set; }

        /// <summary>Keep out of the sea and lakes.</summary>
        public bool DryOnly { get; set; } = true;

        /// <summary>Ground layers to keep off (streets, yards): a spot is refused where any of them shows half or more.</summary>
        public IReadOnlyList<RecordId> AvoidGrounds { get; set; } = Array.Empty<RecordId>();

        /// <summary>The ground layer unpainted ground shows, so avoiding it avoids unpainted ground too.</summary>
        public RecordId Unpainted { get; set; }

        /// <summary>The share of an avoided ground at which a spot is refused.</summary>
        public const float AvoidShare = 0.5f;
    }

    /// <summary>
    /// Picks spots inside a <see cref="GroundShape"/> by <see cref="ScatterRules"/>, engine-free: random points tried
    /// one by one (dart throwing), each kept if its ground fits the rules and nothing kept or already standing is
    /// closer than the spacing. Clustering thins the ground where <see cref="TerrainNoise"/> is low, so the spots
    /// gather in groves with clearings between. It says why spots were turned down, so a caller can loosen the rule
    /// that held it back.
    /// </summary>
    public sealed class Scattering
    {
        /// <summary>Tries per spot asked for before it gives up on finding more.</summary>
        const int TriesPerSpot = 40;

        readonly List<Vector2> _spots = new List<Vector2>();

        /// <param name="standing">Where things already stand, which the spots keep their spacing from.</param>
        public Scattering(GroundShape area, ScatterRules rules, TerrainGrid terrain, WaterMap water, IEnumerable<Vector2> standing, Random random, int count)
        {
            if (area == null)
                throw new ArgumentNullException(nameof(area));
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Scatter at least one.");
            if (!(rules.Spacing > 0f))
                throw new ArgumentOutOfRangeException(nameof(rules), rules.Spacing, "The spacing must be positive.");

            var neighbours = new SpatialBins(rules.Spacing);
            foreach (var point in standing ?? Array.Empty<Vector2>())
                neighbours.Add(point);

            // Clusters about six spacings across, so a grove holds a handful of things.
            var groveSize = rules.Spacing * 6f;
            var tries = count * TriesPerSpot;
            for (var i = 0; i < tries && _spots.Count < count; i++)
            {
                var point = new Vector2(
                    area.Min.X + (float)random.NextDouble() * area.Span.X,
                    area.Min.Y + (float)random.NextDouble() * area.Span.Y);
                if (area.SignedDistance(point) > 0f)
                    continue;

                var height = terrain.HeightAt(point);
                var slope = terrain.SlopeAt(point);
                if (height == null || slope == null)
                {
                    NoLand++;
                    continue;
                }

                if (slope < rules.MinSlope || slope > rules.MaxSlope)
                {
                    TooSteepOrFlat++;
                    continue;
                }

                if ((rules.MinHeight.HasValue && height < rules.MinHeight) || (rules.MaxHeight.HasValue && height > rules.MaxHeight))
                {
                    OutOfHeight++;
                    continue;
                }

                if (rules.DryOnly && water != null && water.DepthAt(point) is float depth && depth > 0f)
                {
                    UnderWater++;
                    continue;
                }

                if (OnAvoided(point, rules, terrain))
                {
                    AvoidedGround++;
                    continue;
                }

                if (rules.Clustering > 0f)
                {
                    // Where the noise is high, everything is kept; where it is low, less and less as clustering grows.
                    var grove = Math.Max(0f, Math.Min(1f, 0.5f + TerrainNoise.At(point, groveSize, 2, 4) * 2f));
                    if (random.NextDouble() > 1f - rules.Clustering * (1f - grove))
                    {
                        Clearing++;
                        continue;
                    }
                }

                if (neighbours.AnyWithin(point, rules.Spacing))
                {
                    Crowded++;
                    continue;
                }

                neighbours.Add(point);
                _spots.Add(point);
            }
        }

        /// <summary>The spots picked, in the order they were found.</summary>
        public IReadOnlyList<Vector2> Spots => _spots;

        /// <summary>Tries that found no land.</summary>
        public int NoLand { get; }

        /// <summary>Tries on ground outside the slope rule.</summary>
        public int TooSteepOrFlat { get; }

        /// <summary>Tries on ground outside the height rule.</summary>
        public int OutOfHeight { get; }

        /// <summary>Tries under water.</summary>
        public int UnderWater { get; }

        /// <summary>Tries thinned out between clusters.</summary>
        public int Clearing { get; }

        /// <summary>Tries too close to a spot or to something standing.</summary>
        public int Crowded { get; }

        /// <summary>Tries on a ground the rules keep off.</summary>
        public int AvoidedGround { get; }

        static bool OnAvoided(Vector2 point, ScatterRules rules, TerrainGrid terrain)
        {
            foreach (var layer in rules.AvoidGrounds)
            {
                if (terrain.ShareAt(point, layer, rules.Unpainted) >= ScatterRules.AvoidShare)
                    return true;
            }

            return false;
        }

        // Points binned by cells one spacing wide, so "is anything within the spacing" reads nine cells.
        sealed class SpatialBins
        {
            readonly float _cell;
            readonly Dictionary<(int, int), List<Vector2>> _bins = new Dictionary<(int, int), List<Vector2>>();

            public SpatialBins(float cell)
            {
                _cell = cell;
            }

            public void Add(Vector2 point)
            {
                var key = Key(point);
                if (!_bins.TryGetValue(key, out var bin))
                    _bins.Add(key, bin = new List<Vector2>());
                bin.Add(point);
            }

            public bool AnyWithin(Vector2 point, float distance)
            {
                var (cx, cz) = Key(point);
                var limit = distance * distance;
                for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (!_bins.TryGetValue((cx + dx, cz + dz), out var bin))
                        continue;
                    foreach (var other in bin)
                    {
                        if (Vector2.DistanceSquared(point, other) < limit)
                            return true;
                    }
                }

                return false;
            }

            (int, int) Key(Vector2 point) => ((int)Math.Floor(point.X / _cell), (int)Math.Floor(point.Y / _cell));
        }
    }
}
