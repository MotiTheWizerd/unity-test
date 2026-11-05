using System;
using System.Linq;
using Forge.Data.Records;
using Forge.Data.Sky;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The time the editor shows: a slider over the 24 hours, the clock as HH:MM, Run to let time pass, and the day,
    /// which sets the season (under the space's climate) and the moons' phases, both shown below it with today's
    /// sunrise and sunset.
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
        readonly Label _date;
        readonly Label _moons;
        (int Hour, ClimateRecord Climate)? _calendarShown;

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
            _date = PanelElements.Required<Label>(root, "time-date");
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
            ShowCalendar(clock);
        }

        // Seasons and phases last days, so they are worked out again once per game hour (or when the climate
        // changes), not every frame.
        void ShowCalendar(WorldClock clock)
        {
            var hourOfTheWorld = clock.Day * (int)WorldClock.HoursPerDay + (int)clock.Hour;
            var climate = _context.ActiveClimate;
            if (_calendarShown is (int hour, var shownClimate) && hour == hourOfTheWorld && ReferenceEquals(shownClimate, climate))
                return;

            _calendarShown = (hourOfTheWorld, climate);
            var sky = _context.Sky;
            var sun = $"Sun {WorldClock.Format(sky.Cycle.Sunrise)}–{WorldClock.Format(sky.Cycle.Sunset)}";
            var season = climate?.SeasonOn(clock.Day);
            PanelElements.SetText(_date, season is SeasonTime time
                ? $"{Capitalized(time.Describe())}, year {time.Year}. {sun}"
                : $"No climate: the same day all year. {sun}");
            PanelElements.SetText(_moons, string.Join("\n", sky.Moons.Select(moon => $"{moon.Moon.Name}: {moon.PhaseName}")));
        }

        static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
