using System;
using Forge.EditorApp.UI.Controls;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// A button that opens a page of a <see cref="ForgeSideDock"/>: it opens the dock on the page, turns to it if another
    /// page is on show, and closes the dock if it already shows it (<see cref="ForgeSideDock.Toggle"/>). It is lit while
    /// its page is on show, and, given a <c>busy</c> test, pulses while that holds, so a closed dock still says so.
    /// </summary>
    public sealed class DockPageToggle : IPanelSection
    {
        readonly ForgeSideDock _dock;
        readonly string _page;
        readonly Func<bool> _busy;
        readonly ForgeButton _button;

        /// <param name="button">The button's name in the panel.</param>
        /// <param name="busy">Whether the page's work is running (the button pulses); null for never.</param>
        public DockPageToggle(VisualElement root, string button, ForgeSideDock dock, string page, Func<bool> busy = null)
        {
            _dock = dock ?? throw new ArgumentNullException(nameof(dock));
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _busy = busy;
            _button = PanelElements.Required<ForgeButton>(root, button);
            _button.clicked += () => _dock.Toggle(_page);
        }

        public void Refresh()
        {
            PanelElements.SetActive(_button, _dock.IsOpen && _dock.Current == _page);
            if (_busy != null)
                _button.busy = _busy();
        }
    }
}
