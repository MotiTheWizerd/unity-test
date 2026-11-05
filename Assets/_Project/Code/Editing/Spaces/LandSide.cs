using System;
using System.Collections.Generic;
using Forge.Data.Terrain;

namespace Forge.Editing.Spaces
{
    /// <summary>A side of a worldspace's land: north is +Z, east is +X.</summary>
    public enum LandSide
    {
        North,
        South,
        East,
        West,
    }

    public static class LandSides
    {
        /// <summary>Every side, north, south, east, west.</summary>
        public static readonly IReadOnlyList<LandSide> All = new[] { LandSide.North, LandSide.South, LandSide.East, LandSide.West };

        /// <summary>
        /// Parses one side ("north" / "n" and the like) or every side ("all"), any case; false for anything else.
        /// </summary>
        public static bool TryParseSides(string text, out IReadOnlyList<LandSide> sides)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "all": case "every": case "all sides":
                    sides = All;
                    return true;
            }

            var parsed = TryParse(text, out var side);
            sides = parsed ? new[] { side } : null;
            return parsed;
        }

        /// <summary>The chunk step outward across <paramref name="side"/>.</summary>
        public static ChunkCoord Outward(this LandSide side)
        {
            switch (side)
            {
                case LandSide.North: return new ChunkCoord(0, 1);
                case LandSide.South: return new ChunkCoord(0, -1);
                case LandSide.East: return new ChunkCoord(1, 0);
                case LandSide.West: return new ChunkCoord(-1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        /// <summary>"north", "south", "east" or "west".</summary>
        public static string Word(this LandSide side) => side.ToString().ToLowerInvariant();

        /// <summary>Parses "north" / "n" and the like, any case; false for anything else.</summary>
        public static bool TryParse(string text, out LandSide side)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "north": case "n": side = LandSide.North; return true;
                case "south": case "s": side = LandSide.South; return true;
                case "east": case "e": side = LandSide.East; return true;
                case "west": case "w": side = LandSide.West; return true;
                default: side = default; return false;
            }
        }
    }
}
