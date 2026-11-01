using System;

namespace Forge.Data.Terrain
{
    /// <summary>Integer grid position of a terrain chunk.</summary>
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public ChunkCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }

        public int Z { get; }

        public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public override string ToString() => $"({X}, {Z})";

        public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);

        public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);
    }
}
