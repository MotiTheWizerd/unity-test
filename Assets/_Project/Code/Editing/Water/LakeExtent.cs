using System;
using System.Collections.Generic;

namespace Forge.Editing.Water
{
    /// <summary>
    /// Where one lake's water lies: the terrain samples (global grid, as <see cref="Terrain.TerrainGrid"/>) whose ground
    /// is below its level, connected to its point. A lake that would run off the land or grow past
    /// <see cref="LakeFill.MaxSamples"/> <see cref="Spills"/> and holds no water.
    /// </summary>
    public sealed class LakeExtent
    {
        public static readonly LakeExtent Dry = new LakeExtent(new HashSet<long>(), 0f, false);
        public static readonly LakeExtent Spilled = new LakeExtent(new HashSet<long>(), 0f, true);

        readonly HashSet<long> _wet;

        internal LakeExtent(HashSet<long> wet, float deepest, bool spills)
        {
            _wet = wet ?? throw new ArgumentNullException(nameof(wet));
            Deepest = deepest;
            Spills = spills;
            if (wet.Count == 0)
                return;

            MinX = MinZ = int.MaxValue;
            MaxX = MaxZ = int.MinValue;
            foreach (var key in wet)
            {
                var (gx, gz) = Unpack(key);
                MinX = Math.Min(MinX, gx);
                MaxX = Math.Max(MaxX, gx);
                MinZ = Math.Min(MinZ, gz);
                MaxZ = Math.Max(MaxZ, gz);
            }
        }

        /// <summary>The water would run off the land (or grow too large) at this level, so there is none.</summary>
        public bool Spills { get; }

        /// <summary>How many terrain samples are under water.</summary>
        public int SampleCount => _wet.Count;

        public bool IsEmpty => _wet.Count == 0;

        /// <summary>The deepest water, in meters.</summary>
        public float Deepest { get; }

        /// <summary>Bounds of the wet samples (inclusive); meaningless when <see cref="IsEmpty"/>.</summary>
        public int MinX { get; }

        public int MaxX { get; }

        public int MinZ { get; }

        public int MaxZ { get; }

        public bool IsWet(int gx, int gz) => _wet.Contains(Pack(gx, gz));

        /// <summary>
        /// Whether water covers part of the grid cell whose lower corner is (<paramref name="gx"/>, <paramref name="gz"/>):
        /// any of its four corner samples is wet. The surface is drawn over these cells, and the land hides it where it rises above.
        /// </summary>
        public bool CoversCell(int gx, int gz) =>
            IsWet(gx, gz) || IsWet(gx + 1, gz) || IsWet(gx, gz + 1) || IsWet(gx + 1, gz + 1);

        /// <summary>
        /// The cells the surface covers (<see cref="CoversCell"/>) as runs along x, row by row from south to north:
        /// (z of the row, first x, last x), each cell's lower corner in global samples. One quad per run draws the lake.
        /// </summary>
        public IEnumerable<(int Z, int FromX, int ToX)> CellRuns()
        {
            if (IsEmpty)
                yield break;

            // A cell is covered when one of its corners is wet, so cells start one before the first wet sample.
            for (var gz = MinZ - 1; gz <= MaxZ; gz++)
            {
                int? from = null;
                for (var gx = MinX - 1; gx <= MaxX + 1; gx++)
                {
                    var covered = gx <= MaxX && CoversCell(gx, gz);
                    if (covered && from == null)
                        from = gx;
                    else if (!covered && from.HasValue)
                    {
                        yield return (gz, from.Value, gx - 1);
                        from = null;
                    }
                }
            }
        }

        internal static long Pack(int gx, int gz) => ((long)gx << 32) | (uint)gz;

        static (int Gx, int Gz) Unpack(long key) => ((int)(key >> 32), (int)(key & 0xFFFFFFFF));
    }
}
