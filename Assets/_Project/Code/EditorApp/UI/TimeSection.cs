using System;
using Forge.Data.Sky;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>The time of day the editor shows: a slider over the 24 hours, the clock as HH:MM, and Run to let time pass.</summary>
    public sealed class TimeSection : IPanelSection
    {
        // Close enough that a running clock doesn't fight the slider; one game minute.
        const float Tolerance = 1f / 60f;

        readonly EditorContext _context;
        readonly Slider _hour;
        readonly Label _readout;
        readonly Toggle _run;

        public TimeSection(VisualElement root, EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hour = PanelElements.Required<Slider>(root, "time-hour");
            _hour.lowValue = 0f;
            _hour.highValue = WorldClock.HoursPerDay;
            _hour.RegisterValueChangedCallback(change => _context.Clock.Hour = change.newValue);
            _readout = PanelElements.Required<Label>(root, "time-readout");
            _run = PanelElements.Required<Toggle>(root, "time-run");
            _run.label = $"Run ({WorldClock.DefaultScale:0}×)";
            _run.RegisterValueChangedCallback(change => _context.Clock.Running = change.newValue);
        }

        public void Refresh()
        {
            var clock = _context.Clock;
            if (Math.Abs(_hour.value - clock.Hour) > Tolerance)
                _hour.SetValueWithoutNotify(clock.Hour);
            PanelElements.SetText(_readout, WorldClock.Format(clock.Hour));
            if (_run.value != clock.Running)
                _run.SetValueWithoutNotify(clock.Running);
        }
    }
}
