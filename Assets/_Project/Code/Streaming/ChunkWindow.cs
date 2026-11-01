using System;
using System.Collections.Generic;
using Forge.Data.Terrain;

namespace Forge.Streaming
{
    /// <summary>
    /// Decides which chunks should be loaded around a focus chunk. Chunks load
    /// within <see cref="LoadRadius"/> and unload only beyond
    /// <see cref="UnloadRadius"/>. The gap between the two stops chunks on the
    /// border from loading and unloading over and over as the focus moves back and forth.
    /// </summary>
    /// <remarks>Distances are Euclidean, in chunks, between chunk coordinates.</remarks>
    public sealed class ChunkWindow
    {
        public ChunkWindow(int loadRadius, int unloadRadius)
        {
            if (loadRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(loadRadius), loadRadius, "Load radius must not be negative.");
            if (unloadRadius < loadRadius)
                throw new ArgumentOutOfRangeException(nameof(unloadRadius), unloadRadius, "Unload radius must be at least the load radius.");

            LoadRadius = loadRadius;
            UnloadRadius = unloadRadius;
        }

        public int LoadRadius { get; }

        public int UnloadRadius { get; }

        /// <summary>
        /// Chunks to unload (outside the unload radius) and to load (inside the
        /// load radius and not yet loaded). Loads are ordered nearest first, then
        /// by Z and X, so a streamer with a time budget fills in the closest chunks first.
        /// </summary>
        public StreamingPlan Plan(ChunkCoord focus, IReadOnlyCollection<ChunkCoord> loaded)
        {
            if (loaded == null)
                throw new ArgumentNullException(nameof(loaded));

            var unloadLimit = UnloadRadius * UnloadRadius;
            var loadedSet = new HashSet<ChunkCoord>();
            var unload = new List<ChunkCoord>();
            foreach (var coord in loaded)
            {
                if (DistanceSquared(focus, coord) > unloadLimit)
                    unload.Add(coord);
                else
                    loadedSet.Add(coord);
            }

            var loadLimit = LoadRadius * LoadRadius;
            var load = new List<ChunkCoord>();
            for (var dz = -LoadRadius; dz <= LoadRadius; dz++)
            for (var dx = -LoadRadius; dx <= LoadRadius; dx++)
            {
                if (dx * dx + dz * dz > loadLimit)
                    continue;
                var coord = new ChunkCoord(focus.X + dx, focus.Z + dz);
                if (!loadedSet.Contains(coord))
                    load.Add(coord);
            }

            load.Sort((a, b) =>
            {
                var byDistance = DistanceSquared(focus, a).CompareTo(DistanceSquared(focus, b));
                if (byDistance != 0)
                    return byDistance;
                return a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
            });

            return new StreamingPlan(load, unload);
        }

        static long DistanceSquared(ChunkCoord a, ChunkCoord b)
        {
            long dx = a.X - b.X;
            long dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }
    }
}
