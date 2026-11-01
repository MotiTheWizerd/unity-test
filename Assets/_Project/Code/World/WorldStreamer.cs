using System;
using System.Diagnostics;
using Forge.Data.Terrain;
using Forge.Streaming;
using UnityEngine;

namespace Forge.World
{
    /// <summary>
    /// Loads and unloads chunks of a <see cref="WorldView"/> around a moving focus
    /// (usually the camera). Unloads happen at once. Loads run nearest first
    /// within a per-frame time budget, so crossing into new terrain spreads the
    /// work over several frames instead of one long hitch.
    /// </summary>
    public sealed class WorldStreamer
    {
        readonly WorldView _view;
        readonly ChunkWindow _window;
        readonly double _frameBudgetMs;
        ChunkCoord? _lastFocus;

        /// <param name="frameBudgetMs">
        /// Loading time allowed per frame. At least one chunk always loads, and
        /// another starts only if the average chunk build time still fits.
        /// </param>
        public WorldStreamer(WorldView view, ChunkWindow window, double frameBudgetMs)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _window = window ?? throw new ArgumentNullException(nameof(window));
            if (!(frameBudgetMs > 0))
                throw new ArgumentOutOfRangeException(nameof(frameBudgetMs), frameBudgetMs, "Frame budget must be positive.");
            _frameBudgetMs = frameBudgetMs;
        }

        public StreamingStats Stats { get; } = new StreamingStats();

        public ChunkWindow Window => _window;

        /// <summary>Loads the whole window around <paramref name="focus"/> now, ignoring the budget (e.g. at startup).</summary>
        public void LoadAll(Vector3 focus) => Step(focus, double.PositiveInfinity);

        /// <summary>Streams towards <paramref name="focus"/> within the frame budget. Returns true if chunks came or went.</summary>
        public bool Tick(Vector3 focus) => Step(focus, _frameBudgetMs);

        bool Step(Vector3 position, double budgetMs)
        {
            var focus = WorldGrid.ChunkOf(position.x, position.z);
            if (focus == _lastFocus && Stats.PendingLoads == 0)
                return false;
            _lastFocus = focus;

            var step = Stopwatch.StartNew();
            var plan = _window.Plan(focus, _view.LoadedChunks);
            foreach (var coord in plan.Unload)
                _view.UnloadChunk(coord);
            Stats.RecordUnloads(plan.Unload.Count);

            // Start another chunk only if it is expected to fit: checking just the
            // elapsed time would let the last chunk run past the budget every frame.
            var loads = 0;
            while (loads < plan.Load.Count && (loads == 0 || step.Elapsed.TotalMilliseconds + Stats.AverageBuildMs <= budgetMs))
            {
                var started = step.Elapsed.TotalMilliseconds;
                if (_view.LoadChunk(plan.Load[loads]) > 0)
                    Stats.RecordBuild(step.Elapsed.TotalMilliseconds - started);
                loads++;
            }

            var changed = plan.Unload.Count > 0 || loads > 0;
            if (changed)
                Terrain.SetConnectivityDirty();

            Stats.RecordStep(step.Elapsed.TotalMilliseconds, plan.Load.Count - loads);
            return changed;
        }
    }
}
