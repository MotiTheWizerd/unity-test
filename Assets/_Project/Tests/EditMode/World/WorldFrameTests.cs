using Forge.Data.Terrain;
using Forge.World;
using NUnit.Framework;
using UnityEngine;

namespace Forge.Tests.World
{
    public class WorldFrameTests
    {
        GameObject _root;

        [SetUp]
        public void SetUp() => _root = new GameObject("World");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void StartsAtTheWorldOrigin_WithTheRootAtZero()
        {
            _root.transform.position = new Vector3(5f, 5f, 5f);

            var frame = new WorldFrame(_root.transform, 8);

            Assert.That(frame.Origin.Chunk, Is.EqualTo(new ChunkCoord(0, 0)));
            Assert.That(_root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(frame.ToScene(new Vector3(100f, 20f, 300f)), Is.EqualTo(new Vector3(100f, 20f, 300f)));
        }

        [Test]
        public void Follow_NearTheOrigin_ChangesNothing()
        {
            var frame = new WorldFrame(_root.transform, 8);
            var shifts = 0;
            frame.Shifted += _ => shifts++;

            var moved = frame.Follow(new Vector3(8.9f * WorldGrid.ChunkSize, 50f, 0f));

            Assert.That(moved, Is.False);
            Assert.That(shifts, Is.Zero);
            Assert.That(_root.transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Follow_FarAway_MovesTheRoot_AndTellsHowFarTheSceneMoved()
        {
            var frame = new WorldFrame(_root.transform, 8);
            Vector3? shift = null;
            frame.Shifted += delta => shift = delta;
            var focus = new Vector3(20.5f * WorldGrid.ChunkSize, 50f, -3.5f * WorldGrid.ChunkSize);

            var moved = frame.Follow(focus);

            Assert.That(moved, Is.True);
            Assert.That(frame.Origin.Chunk, Is.EqualTo(new ChunkCoord(20, -4)));
            Assert.That(_root.transform.position, Is.EqualTo(new Vector3(-20 * WorldGrid.ChunkSize, 0f, 4 * WorldGrid.ChunkSize)));
            Assert.That(shift, Is.EqualTo(new Vector3(-20 * WorldGrid.ChunkSize, 0f, 4 * WorldGrid.ChunkSize)));
            // The focus now sits inside the origin chunk, and a point under the root reads its world coordinates back.
            Assert.That(frame.ToScene(focus), Is.EqualTo(new Vector3(0.5f * WorldGrid.ChunkSize, 50f, 0.5f * WorldGrid.ChunkSize)));
            Assert.That(frame.ToWorld(frame.ToScene(focus)), Is.EqualTo(focus));
        }

        [Test]
        public void ShadersSeeTheWorldOrigin_AndItFollowsARebase()
        {
            var frame = new WorldFrame(_root.transform, 8);
            Assert.That((Vector3)Shader.GetGlobalVector(WorldFrame.OriginProperty), Is.EqualTo(Vector3.zero));

            frame.Follow(new Vector3(20.5f * WorldGrid.ChunkSize, 50f, -12.5f * WorldGrid.ChunkSize));

            Assert.That((Vector3)Shader.GetGlobalVector(WorldFrame.OriginProperty), Is.EqualTo(frame.ToWorld(Vector3.zero)),
                "Scene zero is where the ground's tiling noise starts, in world coordinates.");
            Assert.That(frame.ToWorld(Vector3.zero), Is.Not.EqualTo(Vector3.zero));
        }

        [Test]
        public void Rays_ConvertTheirOrigin_AndKeepTheirDirection()
        {
            var frame = new WorldFrame(_root.transform, 0);
            frame.Follow(new Vector3(2.5f * WorldGrid.ChunkSize, 0f, 0f));
            var scene = new Ray(new Vector3(1f, 10f, 1f), Vector3.down);

            var world = frame.ToWorld(scene);

            Assert.That(world.origin, Is.EqualTo(new Vector3(2 * WorldGrid.ChunkSize + 1f, 10f, 1f)));
            Assert.That(world.direction, Is.EqualTo(Vector3.down));
            Assert.That(frame.ToScene(world).origin, Is.EqualTo(scene.origin));
        }
    }
}
