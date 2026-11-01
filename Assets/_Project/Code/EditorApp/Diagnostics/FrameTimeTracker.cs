using System;

namespace Forge.EditorApp.Diagnostics
{
    /// <summary>
    /// Tracks frame times for the performance readout: the worst frame of the
    /// last full window, and how many frames were long enough to see as a hitch.
    /// </summary>
    public sealed class FrameTimeTracker
    {
        /// <summary>A frame longer than this (three frames at 60 fps) is counted as a hitch.</summary>
        public const double HitchMs = 50;

        readonly double _windowMs;
        double _windowElapsed;
        double _windowWorst;

        public FrameTimeTracker(double windowMs = 2000)
        {
            if (!(windowMs > 0))
                throw new ArgumentOutOfRangeException(nameof(windowMs), windowMs, "Window must be positive.");
            _windowMs = windowMs;
        }

        /// <summary>Worst frame of the last completed window.</summary>
        public double WorstMs { get; private set; }

        /// <summary>Hitches since the start or the last <see cref="ResetHitches"/>.</summary>
        public int Hitches { get; private set; }

        public void Record(double frameMs)
        {
            if (frameMs > HitchMs)
                Hitches++;

            _windowWorst = Math.Max(_windowWorst, frameMs);
            _windowElapsed += frameMs;
            if (_windowElapsed < _windowMs)
                return;

            WorstMs = _windowWorst;
            _windowWorst = 0;
            _windowElapsed = 0;
        }

        public void ResetHitches() => Hitches = 0;
    }
}
