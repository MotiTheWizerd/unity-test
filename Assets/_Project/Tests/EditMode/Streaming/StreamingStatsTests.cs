using System;
using Forge.Streaming;
using NUnit.Framework;

namespace Forge.Tests.Streaming
{
    public class StreamingStatsTests
    {
        [Test]
        public void TracksAverageMaxAndLastBuild()
        {
            var stats = new StreamingStats();

            stats.RecordBuild(2);
            stats.RecordBuild(6);
            stats.RecordBuild(1);

            Assert.That(stats.ChunksBuilt, Is.EqualTo(3));
            Assert.That(stats.AverageBuildMs, Is.EqualTo(3).Within(1e-9));
            Assert.That(stats.MaxBuildMs, Is.EqualTo(6));
            Assert.That(stats.LastBuildMs, Is.EqualTo(1));
        }

        [Test]
        public void StartsEmpty_AndRejectsNegativeDurations()
        {
            var stats = new StreamingStats();

            Assert.That(stats.AverageBuildMs, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => stats.RecordBuild(-1));
        }
    }
}
