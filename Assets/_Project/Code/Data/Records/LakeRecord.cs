using System;
using Forge.Data.Terrain;

namespace Forge.Data.Records
{
    /// <summary>
    /// A lake or pond in a worldspace: a point in the water (<see cref="X"/>, <see cref="Z"/>) and the height of its
    /// surface. Its shape is not stored: the water fills the hollow around that point up to <see cref="Level"/>, so
    /// it follows the terrain when the shore is sculpted, and a mod that reshapes the land reshapes the lake too.
    /// </summary>
    public sealed class LakeRecord : Record
    {
        /// <summary>Name prefix of the ids new lakes get in the active plugin, e.g. "user:lake/000001".</summary>
        public const string IdPrefix = "lake";

        public LakeRecord(RecordId id, RecordId space, float x, float z, float level)
            : base(id)
        {
            if (space.IsDefault)
                throw new ArgumentException("The space the lake lies in is required.", nameof(space));
            if (!IsFinite(x) || !IsFinite(z))
                throw new ArgumentOutOfRangeException(nameof(x), $"The lake's point ({x}, {z}) must be finite.");
            if (!(level >= 0f && level <= WorldGrid.MaxTerrainHeight))
                throw new ArgumentOutOfRangeException(nameof(level), level, $"Lake level must be within [0, {WorldGrid.MaxTerrainHeight}] m.");

            Space = space;
            X = x;
            Z = z;
            Level = level;
        }

        /// <summary>The worldspace the lake lies in.</summary>
        public RecordId Space { get; }

        /// <summary>East-west position of a point in the water, in meters.</summary>
        public float X { get; }

        /// <summary>North-south position of a point in the water, in meters.</summary>
        public float Z { get; }

        /// <summary>Height of the lake's surface, in meters.</summary>
        public float Level { get; }

        public LakeRecord WithLevel(float level) => new LakeRecord(Id, Space, X, Z, level);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
