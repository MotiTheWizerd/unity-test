using System;
using Forge.World;

namespace Forge.EditorApp.Diagnostics
{
    /// <summary>
    /// Everything the performance readout shows, in one place: startup phase
    /// timings, streaming stats and frame times. Filled in by the bootstrap.
    /// </summary>
    public sealed class EditorDiagnostics
    {
        public EditorDiagnostics(WorldView view, WorldStreamer streamer, FrameTimeTracker frames)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Streamer = streamer ?? throw new ArgumentNullException(nameof(streamer));
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
        }

        public WorldView View { get; }

        public WorldStreamer Streamer { get; }

        public FrameTimeTracker Frames { get; }

        /// <summary>Reading and parsing every plugin file.</summary>
        public double LoadPluginsMs { get; private set; }

        /// <summary>Resolving the load order into one world (the edit session's first resolve).</summary>
        public double ResolveMs { get; private set; }

        /// <summary>Indexing the world and building every chunk of the first window.</summary>
        public double FirstViewMs { get; private set; }

        public void RecordStartup(double loadPluginsMs, double resolveMs, double firstViewMs)
        {
            LoadPluginsMs = loadPluginsMs;
            ResolveMs = resolveMs;
            FirstViewMs = firstViewMs;
        }
    }
}
