using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Editing.Spaces
{
    /// <summary>
    /// The chunk rectangle a worldspace's land fills: from its lowest to its highest chunk coordinate on each axis.
    /// Land need not fill the rectangle (it may have been extended unevenly); this is its reach.
    /// </summary>
    public readonly struct LandBounds
    {
        public LandBounds(int minX, int minZ, int maxX, int maxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }

        /// <summary>Chunks across, west to east.</summary>
        public int Width => MaxX - MinX + 1;

        /// <summary>Chunks across, south to north.</summary>
        public int Depth => MaxZ - MinZ + 1;

        /// <summary>The land's reach in meters, west to east.</summary>
        public float WidthMeters => Width * WorldGrid.ChunkSize;

        /// <summary>The land's reach in meters, south to north.</summary>
        public float DepthMeters => Depth * WorldGrid.ChunkSize;

        /// <summary>The bounds of <paramref name="space"/>'s chunks; null when it has no land.</summary>
        public static LandBounds? Of(IEnumerable<ChunkCoord> chunks)
        {
            var any = false;
            int minX = 0, minZ = 0, maxX = 0, maxZ = 0;
            foreach (var coord in chunks)
            {
                if (!any)
                {
                    minX = maxX = coord.X;
                    minZ = maxZ = coord.Z;
                    any = true;
                    continue;
                }

                if (coord.X < minX) minX = coord.X;
                if (coord.X > maxX) maxX = coord.X;
                if (coord.Z < minZ) minZ = coord.Z;
                if (coord.Z > maxZ) maxZ = coord.Z;
            }

            return any ? new LandBounds(minX, minZ, maxX, maxZ) : (LandBounds?)null;
        }

        public static LandBounds? Of(IRecordStore records, RecordId space) =>
            Of(records.OfType<TerrainChunkRecord>().Where(c => c.Space == space).Select(c => c.Coord));

        /// <summary>E.g. "16×16 chunks (1,024 m)" or "20×16 chunks (1,280 × 1,024 m)".</summary>
        public override string ToString() =>
            Width == Depth
                ? FormattableString.Invariant($"{Width}×{Depth} chunks ({WidthMeters:#,0} m)")
                : FormattableString.Invariant($"{Width}×{Depth} chunks ({WidthMeters:#,0} × {DepthMeters:#,0} m)");
    }
}
