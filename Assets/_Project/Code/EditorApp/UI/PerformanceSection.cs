using System;
using Forge.EditorApp.Diagnostics;
using Forge.EditorApp.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>Live streaming, frame and edit timings; only refreshed while the section is open, a few times a second.</summary>
    public sealed class PerformanceSection : IPanelSection
    {
        const float RefreshInterval = 0.25f;

        readonly EditorContext _context;
        readonly EditorDiagnostics _diagnostics;
        readonly ForgeSection _section;
        readonly Label _text;
        float _nextRefresh;

        public PerformanceSection(VisualElement root, EditorContext context, EditorDiagnostics diagnostics)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _section = PanelElements.Required<ForgeSection>(root, "performance");
            _text = PanelElements.Required<Label>(root, "performance-text");
        }

        public void Refresh()
        {
            if (_section.collapsed || Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            var d = _diagnostics;
            var stats = d.Streamer.Stats;
            PanelElements.SetText(_text,
                $"Chunks loaded: {d.View.LoadedChunks.Count} (radius {d.Streamer.Window.LoadRadius})   pending: {stats.PendingLoads}\n" +
                $"Objects shown: {d.View.PresentedCount}\n" +
                $"Chunk build: avg {stats.AverageBuildMs:0.0} ms, max {stats.MaxBuildMs:0.0} ms\n" +
                $"Built {stats.ChunksBuilt}, unloaded {stats.ChunksUnloaded}, last step {stats.LastStepMs:0.0} ms\n" +
                $"Worst frame (2 s): {d.Frames.WorstMs:0} ms   hitches: {d.Frames.Hitches}\n" +
                $"Last edit: {_context.LastEditMs:0.0} ms\n" +
                $"Startup: load {d.LoadPluginsMs:0} + resolve {d.ResolveMs:0} + view {d.FirstViewMs:0} ms");
        }
    }
}
