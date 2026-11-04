using System;
using System.Linq;
using Forge.Data.Sky;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The time the editor shows: a slider over the 24 hours, the clock as HH:MM, Run to let time pass, and the day,
    /// which sets the moons' phases (shown below it).
    /// </summary>
    public sealed class TimeSection : IPanelSection
    {
        // Close enough that a running clock doesn't fight the slider; one game minute.
        const float Tolerance = 1f / 60f;

        readonly EditorContext _context;
        readonly Slider _hour;
        readonly Label _readout;
        readonly Toggle _run;
        readonly IntegerField _day;
        readonly Label _moons;
        int _moonsShownAt = -1;

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
            _day = PanelElements.Required<IntegerField>(root, "time-day");
            _day.RegisterValueChangedCallback(change => _context.Clock.Day = Math.Max(0, change.newValue));
            _moons = PanelElements.Required<Label>(root, "time-moons");
        }

        public void Refresh()
        {
            var clock = _context.Clock;
            if (Math.Abs(_hour.value - clock.Hour) > Tolerance)
                _hour.SetValueWithoutNotify(clock.Hour);
            PanelElements.SetText(_readout, WorldClock.Format(clock.Hour));
            if (_run.value != clock.Running)
                _run.SetValueWithoutNotify(clock.Running);
            // Not while it is being typed in, or a running clock would overwrite the typing.
            if (_day.value != clock.Day && _day.focusController?.focusedElement != _day)
                _day.SetValueWithoutNotify(clock.Day);
            ShowMoons(clock);
        }

        // A phase lasts days, so the names are worked out again once per game hour, not every frame.
        void ShowMoons(WorldClock clock)
        {
            var hourOfTheWorld = clock.Day * (int)WorldClock.HoursPerDay + (int)clock.Hour;
            if (hourOfTheWorld == _moonsShownAt)
                return;

            _moonsShownAt = hourOfTheWorld;
            PanelElements.SetText(_moons, string.Join("\n", _context.Sky.Moons.Select(moon => $"{moon.Moon.Name}: {moon.PhaseName}")));
        }
    }
}
