using System;
using System.Numerics;
using Forge.Data.Terrain;

namespace Forge.Streaming
{
    /// <summary>
    /// Where the scene's origin stands in the world: the corner of one chunk. Records keep their true world
    /// coordinates however big the map gets; the scene is laid out relative to this origin, so what the engine
    /// renders and simulates stays near zero, where 32-bit floats are precise. <see cref="Follow"/> moves the origin
    /// once the focus (the camera) is far enough from it, always by whole chunks, so the scene is rebased rarely and
    /// by exact multiples of the chunk size. Heights are never rebased: the origin's y is always 0.
    /// </summary>
    public readonly struct SceneOrigin : IEquatable<SceneOrigin>
    {
        public SceneOrigin(ChunkCoord chunk)
        {
            Chunk = chunk;
        }

        /// <summary>The origin at the world's own origin: scene and world coordinates are the same.</summary>
        public static SceneOrigin Zero => default;

        /// <summary>The chunk whose corner the scene's origin stands on.</summary>
        public ChunkCoord Chunk { get; }

        /// <summary>The origin's world position.</summary>
        public Vector3 Position => new Vector3(Chunk.X * WorldGrid.ChunkSize, 0f, Chunk.Z * WorldGrid.ChunkSize);

        public Vector3 ToScene(Vector3 world) => world - Position;

        public Vector3 ToWorld(Vector3 scene) => scene + Position;

        /// <summary>
        /// The origin to use with the focus in chunk <paramref name="focus"/>: this one while the focus is within
        /// <paramref name="maxDistance"/> chunks of it along both axes, else the focus's own chunk.
        /// </summary>
        public SceneOrigin Follow(ChunkCoord focus, int maxDistance)
        {
            if (maxDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "The distance must not be negative.");

            var far = Math.Abs(focus.X - Chunk.X) > maxDistance || Math.Abs(focus.Z - Chunk.Z) > maxDistance;
            return far ? new SceneOrigin(focus) : this;
        }

        /// <summary>Same as <see cref="Follow(ChunkCoord, int)"/> for a focus at world position <paramref name="focus"/>.</summary>
        public SceneOrigin Follow(Vector3 focus, int maxDistance) => Follow(WorldGrid.ChunkOf(focus.X, focus.Z), maxDistance);

        public bool Equals(SceneOrigin other) => Chunk == other.Chunk;

        public override bool Equals(object obj) => obj is SceneOrigin other && Equals(other);

        public override int GetHashCode() => Chunk.GetHashCode();

        public override string ToString() => $"origin at chunk {Chunk}";

        public static bool operator ==(SceneOrigin left, SceneOrigin right) => left.Equals(right);

        public static bool operator !=(SceneOrigin left, SceneOrigin right) => !left.Equals(right);
    }
}
