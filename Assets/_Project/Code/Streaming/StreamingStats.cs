using System;

namespace Forge.Streaming
{
    /// <summary>
    /// Running measurements of a streamer, so the cost of streaming can be read
    /// in the running editor. Only chunks with content count towards the build
    /// times; empty chunks cost nothing and would hide the real cost.
    /// </summary>
    public sealed class StreamingStats
    {
        double _totalBuildMs;

        /// <summary>Chunks with content that have been loaded since the start.</summary>
        public int ChunksBuilt { get; private set; }

        public int ChunksUnloaded { get; private set; }

        /// <summary>Chunks the window wants that are still waiting for frame budget.</summary>
        public int PendingLoads { get; private set; }

        public double LastBuildMs { get; private set; }

        public double MaxBuildMs { get; private set; }

        public double AverageBuildMs => ChunksBuilt == 0 ? 0 : _totalBuildMs / ChunksBuilt;

        /// <summary>Time spent in the most recent streaming step that did work (loads and unloads).</summary>
        public double LastStepMs { get; private set; }

        public void RecordStep(double milliseconds, int pendingLoads)
        {
            LastStepMs = milliseconds;
            PendingLoads = pendingLoads;
        }

        public void RecordBuild(double milliseconds)
        {
            if (milliseconds < 0 || double.IsNaN(milliseconds))
                throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "Duration must be a non-negative number.");

            ChunksBuilt++;
            _totalBuildMs += milliseconds;
            LastBuildMs = milliseconds;
            MaxBuildMs = Math.Max(MaxBuildMs, milliseconds);
        }

        public void RecordUnloads(int count) => ChunksUnloaded += count;
    }
}
