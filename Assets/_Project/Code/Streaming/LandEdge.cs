using System;

namespace Forge.Streaming
{
    /// <summary>
    /// One side of a chunk with no land beyond it: where the land ends, at the chunk's full height resolution, so a
    /// skirt hung from it meets the real terrain's edge exactly. Positions are meters in its <see cref="FarTile"/>.
    /// </summary>
    public sealed class LandEdge
    {
        public LandEdge(float startX, float startZ, int directionX, int directionZ, float spacing, float[] heights)
        {
            if (Math.Abs(directionX) + Math.Abs(directionZ) != 1)
                throw new ArgumentException("An edge runs along X or Z, one way.");
            if (!(spacing > 0f))
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "Spacing must be positive.");
            if (heights == null || heights.Length < 2)
                throw new ArgumentException("An edge has at least two samples.", nameof(heights));

            StartX = startX;
            StartZ = startZ;
            DirectionX = directionX;
            DirectionZ = directionZ;
            Spacing = spacing;
            Heights = heights;
        }

        /// <summary>The first sample's position in the tile, meters.</summary>
        public float StartX { get; }

        public float StartZ { get; }

        /// <summary>
        /// Which way the samples run (one of ±X, ±Z), chosen so the land lies to the left looking along it: the side
        /// facing away from the land is then the same for every edge (the skirt's outside).
        /// </summary>
        public int DirectionX { get; }

        public int DirectionZ { get; }

        /// <summary>Meters between samples.</summary>
        public float Spacing { get; }

        /// <summary>The land's height at each sample, in order along <see cref="DirectionX"/>, <see cref="DirectionZ"/>.</summary>
        public float[] Heights { get; }

        /// <summary>The outward direction: away from the land, at a right angle to the edge.</summary>
        public int OutwardX => DirectionZ;

        public int OutwardZ => -DirectionX;
    }
}
