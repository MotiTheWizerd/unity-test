using System;
using Forge.Data.Records;
using Forge.Data.Terrain;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// Where a placed object is kept: the chunk under its position, in its space. Objects are stored, loaded and
    /// resolved a block at a time, so a world opens without reading its objects and only the land in use has them in memory.
    /// </summary>
    public readonly struct ObjectBlockKey : IEquatable<ObjectBlockKey>, IComparable<ObjectBlockKey>
    {
        public ObjectBlockKey(RecordId space, ChunkCoord coord)
        {
            if (space.IsDefault)
                throw new ArgumentException("A block belongs to a space.", nameof(space));
            Space = space;
            Coord = coord;
        }

        public RecordId Space { get; }

        public ChunkCoord Coord { get; }

        /// <summary>The block <paramref name="placed"/> belongs to: its space, and the chunk under its position.</summary>
        public static ObjectBlockKey Of(PlacedObjectRecord placed)
        {
            if (placed == null)
                throw new ArgumentNullException(nameof(placed));
            return new ObjectBlockKey(placed.Space, WorldGrid.ChunkOf(placed.Position.X, placed.Position.Z));
        }

        public bool Equals(ObjectBlockKey other) => Space == other.Space && Coord == other.Coord;

        public override bool Equals(object obj) => obj is ObjectBlockKey other && Equals(other);

        public override int GetHashCode() => unchecked(Space.GetHashCode() * 397 ^ Coord.GetHashCode());

        public int CompareTo(ObjectBlockKey other)
        {
            var bySpace = Space.CompareTo(other.Space);
            if (bySpace != 0)
                return bySpace;
            var byX = Coord.X.CompareTo(other.Coord.X);
            return byX != 0 ? byX : Coord.Z.CompareTo(other.Coord.Z);
        }

        public override string ToString() => $"{Space} {Coord}";

        public static bool operator ==(ObjectBlockKey left, ObjectBlockKey right) => left.Equals(right);

        public static bool operator !=(ObjectBlockKey left, ObjectBlockKey right) => !left.Equals(right);
    }
}
