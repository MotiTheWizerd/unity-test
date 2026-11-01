using System;
using System.Linq;
using Forge.Data.Terrain;
using Forge.Streaming;
using NUnit.Framework;

namespace Forge.Tests.Streaming
{
    public class ChunkWindowTests
    {
        static readonly ChunkCoord Origin = new ChunkCoord(0, 0);

        [Test]
        public void FromNothing_LoadsADiscAroundTheFocus_NearestFirst()
        {
            var plan = new ChunkWindow(2, 3).Plan(new ChunkCoord(10, -4), Array.Empty<ChunkCoord>());

            // Radius 2 disc: centre, 4 at distance 1, 4 at √2, 4 at 2 = 13 chunks; (±2, ±1) at √5 are outside.
            Assert.That(plan.Load, Has.Count.EqualTo(13));
            Assert.That(plan.Load[0], Is.EqualTo(new ChunkCoord(10, -4)));
            Assert.That(plan.Load.Take(5).Skip(1), Is.EquivalentTo(new[]
            {
                new ChunkCoord(9, -4), new ChunkCoord(11, -4), new ChunkCoord(10, -5), new ChunkCoord(10, -3),
            }));
            Assert.That(plan.Load, Has.No.Member(new ChunkCoord(12, -2)));
            Assert.That(plan.Unload, Is.Empty);
        }

        [Test]
        public void LoadOrder_IsDeterministic()
        {
            var window = new ChunkWindow(3, 3);

            var first = window.Plan(Origin, Array.Empty<ChunkCoord>()).Load;
            var second = window.Plan(Origin, Array.Empty<ChunkCoord>()).Load;

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void AlreadyLoadedChunks_AreNotLoadedAgain()
        {
            var plan = new ChunkWindow(1, 1).Plan(Origin, new[] { Origin, new ChunkCoord(1, 0) });

            Assert.That(plan.Load, Is.EquivalentTo(new[] { new ChunkCoord(-1, 0), new ChunkCoord(0, -1), new ChunkCoord(0, 1) }));
        }

        [Test]
        public void ChunksBetweenLoadAndUnloadRadius_StayLoaded()
        {
            var window = new ChunkWindow(1, 3);
            var loaded = new[] { new ChunkCoord(3, 0), new ChunkCoord(2, 2), new ChunkCoord(4, 0), new ChunkCoord(3, 1) };

            var plan = window.Plan(Origin, loaded);

            // Distances: 3, √8 ≈ 2.83 (kept), 4 (dropped), √10 ≈ 3.16 (dropped).
            Assert.That(plan.Unload, Is.EquivalentTo(new[] { new ChunkCoord(4, 0), new ChunkCoord(3, 1) }));
            Assert.That(plan.Load, Has.No.Member(new ChunkCoord(3, 0)));
        }

        [Test]
        public void MovingTheFocus_UnloadsBehindAndLoadsAhead()
        {
            var window = new ChunkWindow(1, 1);
            var loaded = window.Plan(Origin, Array.Empty<ChunkCoord>()).Load.ToArray();

            var plan = window.Plan(new ChunkCoord(1, 0), loaded);

            Assert.That(plan.Unload, Is.EquivalentTo(new[] { new ChunkCoord(-1, 0), new ChunkCoord(0, -1), new ChunkCoord(0, 1) }));
            Assert.That(plan.Load, Is.EquivalentTo(new[] { new ChunkCoord(2, 0), new ChunkCoord(1, -1), new ChunkCoord(1, 1) }));
        }

        [Test]
        public void RejectsInvalidRadii()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkWindow(-1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkWindow(3, 2));
            Assert.DoesNotThrow(() => new ChunkWindow(0, 0));
        }
    }
}
