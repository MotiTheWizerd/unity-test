using System;
using Forge.Data.Terrain;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class WorldGridTests
    {
        [TestCase(65, 1f)]
        [TestCase(33, 2f)]
        [TestCase(17, 4f)]
        public void SpacingAndResolution_AreEachOthersInverse(int resolution, float spacing)
        {
            Assert.That(WorldGrid.SpacingOf(resolution), Is.EqualTo(spacing));
            Assert.That(WorldGrid.ResolutionFor(spacing), Is.EqualTo(resolution));
        }

        [TestCase(0f)]
        [TestCase(3f)]
        [TestCase(0.05f)]
        [TestCase(-1f)]
        public void ResolutionFor_RefusesSpacingsThatDoNotFitTheChunk(float spacing)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldGrid.ResolutionFor(spacing));
        }

        [Test]
        public void SpacingOf_RefusesABadResolution()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldGrid.SpacingOf(64));
        }
    }
}
