using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Terrain;
using Forge.Editing.Geometry;

namespace Forge.Editing.Terrain
{
    /// <summary>How a landform's side falls from its spine (top, or bottom when lowered) to its foot.</summary>
    public enum LandformProfile
    {
        /// <summary>Rounded top, easing out at the foot: a hill, a mound, a dune; lowered, a bowl or a U valley.</summary>
        Round,

        /// <summary>Sharp top, steep above and spreading at the foot: a mountain, a crag; lowered, a V valley.</summary>
        Peak,

        /// <summary>Flat over the inner half, then easing down: a mesa, a raised plateau; lowered, a flat-bottomed basin.</summary>
        Plateau,
    }

    /// <summary>
    /// Raises a landform on the ground, or with a negative height lowers one into it: around one point a hill, a
    /// mountain or a hollow, along several a ridge or a valley. The ground rises by the height at the spine and eases
    /// back to untouched ground at half the width from it, by the <see cref="LandformProfile"/>; roughness breaks the
    /// shape up with <see cref="TerrainNoise"/> (wandering slopes, spurs, an uneven crest) so it looks grown rather
    /// than turned. It adds to the ground already there, so landforms stand on slopes and build on each other.
    /// </summary>
    public sealed class LandformSculpting : TerrainEdit
    {
        /// <summary>The widest landform, across its foot, in meters.</summary>
        public const float MaxWidth = 256f;

        /// <summary>The most a landform may span, its width included, east-west or north-south, in meters.</summary>
        public const float MaxSpan = 512f;

        // At roughness 1: how far the slopes wander, as a share of the distance to the foot, and how much the height varies.
        const float Wander = 0.35f;
        const float Bumps = 0.5f;

        /// <param name="store">World to edit; the terrain of <paramref name="space"/> is snapshotted now.</param>
        /// <param name="space">The worldspace whose land is sculpted.</param>
        /// <param name="spine">One point (a hill) or a path (a ridge), in meters.</param>
        /// <param name="width">Across the landform's foot, in meters.</param>
        /// <param name="height">How far the spine rises above the ground there, in meters; negative lowers it.</param>
        /// <param name="profile">How the sides fall.</param>
        /// <param name="roughness">0 for a clean shape, up to 1 for broken, rugged land.</param>
        public LandformSculpting(IRecordStore store, RecordId space, IReadOnlyList<Vector2> spine, float width, float height,
            LandformProfile profile, float roughness)
            : base(store, space)
        {
            if (spine == null || spine.Count == 0)
                throw new ArgumentException("A landform needs a point or a path.", nameof(spine));
            if (!(width > 0f && width <= MaxWidth))
                throw new ArgumentOutOfRangeException(nameof(width), width, $"Width must be within (0, {MaxWidth}] m.");
            if (!(height != 0f && Math.Abs(height) <= WorldGrid.MaxTerrainHeight))
                throw new ArgumentOutOfRangeException(nameof(height), height, $"Height must be non-zero and within ±{WorldGrid.MaxTerrainHeight} m.");
            if (!(roughness >= 0f && roughness <= 1f))
                throw new ArgumentOutOfRangeException(nameof(roughness), roughness, "Roughness must be within [0, 1].");

            Spine = spine.ToArray();
            Width = width;
            Height = height;
            Profile = profile;
            Roughness = roughness;

            var shape = GroundShape.Band(Spine, width);
            if (shape.Span.X > MaxSpan || shape.Span.Y > MaxSpan)
                throw new ArgumentException(FormattableString.Invariant(
                    $"That landform spans {shape.Span.X:0} × {shape.Span.Y:0} m; sculpt at most {MaxSpan:0} m across at a time."));

            // The largest wander and bumps follow the landform's size, and the finest stop at about two samples.
            var half = width / 2f;
            var wanderLength = Math.Max(width / 4f, 2f * Terrain.Spacing);
            var bumpLength = Math.Max(width / 6f, 2f * Terrain.Spacing);

            var updates = new List<(int gx, int gz, float height)>();
            var highest = float.MinValue;
            var lowest = float.MaxValue;
            foreach (var sample in Terrain.SamplesIn(shape.Min, shape.Max))
            {
                var t = (shape.SignedDistance(sample.Ground) + half) / half;
                if (t >= 1f)
                    continue;

                var rise = height * Rise(t, sample.Ground, wanderLength, bumpLength);
                var next = sample.Height + rise;
                if (next > WorldGrid.MaxTerrainHeight || next < 0f)
                    ReachedTheLimit = true;

                next = Math.Max(0f, Math.Min(WorldGrid.MaxTerrainHeight, next));
                MostRaised = Math.Max(MostRaised, next - sample.Height);
                MostLowered = Math.Max(MostLowered, sample.Height - next);
                highest = Math.Max(highest, next);
                lowest = Math.Min(lowest, next);
                updates.Add((sample.Gx, sample.Gz, next));
            }

            if (updates.Count == 0)
                return;

            Highest = highest;
            Lowest = lowest;
            Write(updates);
        }

        public IReadOnlyList<Vector2> Spine { get; }

        public float Width { get; }

        public float Height { get; }

        public LandformProfile Profile { get; }

        public float Roughness { get; }

        /// <summary>The most any ground was raised, in meters.</summary>
        public float MostRaised { get; }

        /// <summary>The most any ground was lowered, in meters.</summary>
        public float MostLowered { get; }

        /// <summary>The highest ground under the landform afterwards, in meters; null when there was no land under it.</summary>
        public float? Highest { get; }

        /// <summary>The lowest ground under the landform afterwards, in meters; null when there was no land under it.</summary>
        public float? Lowest { get; }

        /// <summary>Whether some ground would have gone past the terrain's top or bottom, and stopped there.</summary>
        public bool ReachedTheLimit { get; }

        /// <summary>What the landform is, in a word: hill, hollow, ridge or valley.</summary>
        public string Kind => Spine.Count == 1 ? (Height > 0f ? "hill" : "hollow") : (Height > 0f ? "ridge" : "valley");

        public override string Description => $"Sculpt {Kind}";

        /// <summary>
        /// How much of the height the profile gives at <paramref name="t"/>: 1 at the spine (0), easing to none at the
        /// foot (1).
        /// </summary>
        public static float Fall(LandformProfile profile, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            switch (profile)
            {
                case LandformProfile.Round:
                    return Falloff.Smooth(t);
                case LandformProfile.Peak:
                    return (1f - t) * (1f - t);
                case LandformProfile.Plateau:
                    return t <= 0.5f ? 1f : Falloff.Smooth((t - 0.5f) * 2f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile, null);
            }
        }

        // The share of the height at `ground`, `t` of the way from the spine to the foot. Roughness lets the slope
        // wander in and out (never at the foot, so the landform still meets the land there) and varies the height
        // (which fades with the profile at the foot).
        float Rise(float t, Vector2 ground, float wanderLength, float bumpLength)
        {
            if (Roughness <= 0f)
                return Fall(Profile, t);

            var wander = Roughness * Wander * (1f - t) * TerrainNoise.At(ground, wanderLength, 3, 1);
            var bumps = Roughness * Bumps * TerrainNoise.At(ground, bumpLength, 4, 2);
            return Fall(Profile, t + wander) * (1f + bumps);
        }
    }
}
