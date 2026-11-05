using System;
using System.Numerics;
using Forge.Data.Terrain;
using Forge.Streaming;
using NUnit.Framework;

namespace Forge.Tests.Streaming
{
    public class SceneOriginTests
    {
        [Test]
        public void Zero_LeavesCoordinatesAlone()
        {
            var world = new Vector3(1234.5f, 60f, -987f);

            Assert.That(SceneOrigin.Zero.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(SceneOrigin.Zero.ToScene(world), Is.EqualTo(world));
            Assert.That(SceneOrigin.Zero.ToWorld(world), Is.EqualTo(world));
        }

        [Test]
        public void Position_IsTheChunkCorner_AtHeightZero()
        {
            var origin = new SceneOrigin(new ChunkCoord(3, -2));

            Assert.That(origin.Position, Is.EqualTo(new Vector3(3 * WorldGrid.ChunkSize, 0f, -2 * WorldGrid.ChunkSize)));
        }

        [Test]
        public void Conversions_ShiftByTheOrigin_AndRoundTrip()
        {
            var origin = new SceneOrigin(new ChunkCoord(3, -2));
            var world = new Vector3(200f, 50f, -100f);

            var scene = origin.ToScene(world);

            Assert.That(scene, Is.EqualTo(new Vector3(8f, 50f, 28f)));
            Assert.That(origin.ToWorld(scene), Is.EqualTo(world));
        }

        [Test]
        public void Follow_StaysPut_WhileTheFocusIsWithinReach()
        {
            var origin = new SceneOrigin(new ChunkCoord(10, 10));

            Assert.That(origin.Follow(new ChunkCoord(18, 2), 8), Is.EqualTo(origin));
            Assert.That(origin.Follow(new ChunkCoord(2, 18), 8), Is.EqualTo(origin));
            Assert.That(origin.Follow(new ChunkCoord(10, 10), 0), Is.EqualTo(origin));
        }

        [Test]
        public void Follow_MovesToTheFocusChunk_OncePastReachOnEitherAxis()
        {
            var origin = new SceneOrigin(new ChunkCoord(10, 10));

            Assert.That(origin.Follow(new ChunkCoord(19, 10), 8), Is.EqualTo(new SceneOrigin(new ChunkCoord(19, 10))));
            Assert.That(origin.Follow(new ChunkCoord(10, 1), 8), Is.EqualTo(new SceneOrigin(new ChunkCoord(10, 1))));
        }

        [Test]
        public void Follow_ByWorldPosition_UsesTheChunkUnderIt()
        {
            // 9 chunks east of the origin, well past 8; inside the chunk, not on its corner.
            var focus = new Vector3(9.5f * WorldGrid.ChunkSize, 120f, 0.5f * WorldGrid.ChunkSize);

            var moved = SceneOrigin.Zero.Follow(focus, 8);

            Assert.That(moved.Chunk, Is.EqualTo(new ChunkCoord(9, 0)));
            Assert.That(moved.ToScene(focus), Is.EqualTo(new Vector3(0.5f * WorldGrid.ChunkSize, 120f, 0.5f * WorldGrid.ChunkSize)));
        }

        [Test]
        public void Follow_RejectsANegativeReach() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => SceneOrigin.Zero.Follow(new ChunkCoord(0, 0), -1));
    }
}
