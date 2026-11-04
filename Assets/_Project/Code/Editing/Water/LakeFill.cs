using System;
using System.Collections.Generic;
using System.Numerics;
using Forge.Core.Collections;
using Forge.Editing.Terrain;

namespace Forge.Editing.Water
{
    /// <summary>
    /// How water fills the land around a point, like a paint bucket: every terrain sample below the level that is
    /// connected to the point (through neighbours north, south, east or west) is under water. Engine-free.
    /// </summary>
    public static class LakeFill
    {
        /// <summary>The most samples one lake may cover (about 360 × 360 m at 1 m spacing); a larger one counts as spilling.</summary>
        public const int MaxSamples = 128 * 1024;

        static readonly (int X, int Z)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        /// <summary>
        /// The hollow around <paramref name="point"/>: its ground there, and the highest level water can stand at
        /// before it runs off the land, which is the lowest point of the hollow's rim (or the level at which it would
        /// grow past <see cref="MaxSamples"/>). Null where there is no terrain.
        /// </summary>
        public static LakeBasin? Survey(TerrainGrid terrain, Vector2 point)
        {
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));

            var (seedX, seedZ) = NearestSample(terrain, point);
            if (!(terrain.SampleHeight(seedX, seedZ) is float ground))
                return null;

            // Priority flood: always step to the lowest sample on the edge of what is reached so far. The highest
            // ground stepped over on the way to the land's edge is the rim's lowest point: water up to it stays in.
            var reached = new HashSet<long> { LakeExtent.Pack(seedX, seedZ) };
            var frontier = new MinHeap<(int X, int Z)>();
            frontier.Push(ground, (seedX, seedZ));
            var brim = ground;
            var popped = 0;
            while (frontier.Count > 0 && popped < MaxSamples)
            {
                var (height, (x, z)) = frontier.Pop();
                popped++;
                brim = Math.Max(brim, height);
                foreach (var (dx, dz) in Neighbours)
                {
                    var key = LakeExtent.Pack(x + dx, z + dz);
                    if (reached.Contains(key))
                        continue;
                    if (!(terrain.SampleHeight(x + dx, z + dz) is float next))
                        return new LakeBasin(ground, brim);
                    reached.Add(key);
                    frontier.Push(next, (x + dx, z + dz));
                }
            }

            return new LakeBasin(ground, brim);
        }

        /// <summary>
        /// The water around <paramref name="point"/> at <paramref name="level"/>: <see cref="LakeExtent.Dry"/> if the
        /// ground there is at or above it, an extent that <see cref="LakeExtent.Spills"/> if the water reaches the edge
        /// of the land or grows past <see cref="MaxSamples"/>.
        /// </summary>
        public static LakeExtent Fill(TerrainGrid terrain, Vector2 point, float level)
        {
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));

            var (seedX, seedZ) = NearestSample(terrain, point);
            if (!(terrain.SampleHeight(seedX, seedZ) is float ground) || ground >= level)
                return LakeExtent.Dry;

            var wet = new HashSet<long> { LakeExtent.Pack(seedX, seedZ) };
            var open = new Queue<(int X, int Z)>();
            open.Enqueue((seedX, seedZ));
            var lowest = ground;
            while (open.Count > 0)
            {
                var (x, z) = open.Dequeue();
                foreach (var (dx, dz) in Neighbours)
                {
                    var key = LakeExtent.Pack(x + dx, z + dz);
                    if (wet.Contains(key))
                        continue;
                    if (!(terrain.SampleHeight(x + dx, z + dz) is float height))
                        return LakeExtent.Spilled;
                    if (height >= level)
                        continue;
                    if (wet.Count >= MaxSamples)
                        return LakeExtent.Spilled;
                    wet.Add(key);
                    lowest = Math.Min(lowest, height);
                    open.Enqueue((x + dx, z + dz));
                }
            }

            return new LakeExtent(wet, level - lowest, false);
        }

        /// <summary>The global terrain sample nearest <paramref name="point"/>, where a fill starts.</summary>
        public static (int Gx, int Gz) NearestSample(TerrainGrid terrain, Vector2 point) =>
            ((int)Math.Round(point.X / terrain.Spacing), (int)Math.Round(point.Y / terrain.Spacing));
    }
}
