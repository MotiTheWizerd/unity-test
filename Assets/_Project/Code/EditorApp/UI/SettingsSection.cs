using System.Collections.Generic;
using Forge.EditorApp.UI.Controls;
using Forge.Editing.State;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The settings card: the gear at the end of the top bar's global row toggles it in the middle of the screen, and
    /// stays lit while it is open. A tab bar picks one page (settings-page-{key} in EditorPanel.uxml); the pages hold
    /// the sections that used to live in the left dock, each still run by its own presenter. The top bar's new-space
    /// button opens the card on the Space page when it shows the new-space form there.
    /// </summary>
    public sealed class SettingsSection : IPanelSection, IKeptSection
    {
        public const string Space = "space";
        public const string Time = "time";
        public const string Camera = "camera";
        public const string Performance = "performance";
        public const string General = "general";

        // In order; the first is the page the card opens on.
        static readonly (string Key, string Title)[] Tabs =
        {
            (Space, "Space"),
            (Time, "Time"),
            (Camera, "Camera"),
            (Performance, "Performance"),
            (General, "General"),
        };

        readonly ForgePopup _popup;
        readonly TabBar _tabs = new TabBar();
        readonly Dictionary<string, VisualElement> _pages = new Dictionary<string, VisualElement>();

        public SettingsSection(VisualElement root)
        {
            var button = PanelElements.Required<Button>(root, "settings");
            _popup = PanelElements.Required<ForgePopup>(root, "settings-popup");
            button.clicked += () => _popup.Toggle(button);
            _popup.OpenChanged += open => button.EnableInClassList(PanelElements.ActiveClass, open);

            foreach (var (key, _) in Tabs)
                _pages.Add(key, PanelElements.Required<VisualElement>(root, $"settings-page-{key}"));
            _tabs.SetTabs(Tabs);
            _tabs.Picked += ShowPage;
            PanelElements.Required<VisualElement>(root, "settings-tabs").Add(_tabs);
            ShowPage(_tabs.Current);

            // SpaceSection registered first, so by now the form has just opened or closed.
            var newSpace = PanelElements.Required<Button>(root, "space-new");
            var newSpaceForm = PanelElements.Required<VisualElement>(root, "space-new-form");
            newSpace.clicked += () =>
            {
                if (newSpaceForm.ClassListContains(PanelElements.HiddenClass))
                    return;
                Open(Space, newSpace);
            };
        }

        /// <summary>Opens the card on <paramref name="key"/>'s page.</summary>
        /// <param name="opener">The element that asked; presses on it don't count as outside the card.</param>
        public void Open(string key, VisualElement opener = null)
        {
            _tabs.Select(key);
            ShowPage(key);
            _popup.Open(opener);
        }

        void ShowPage(string key)
        {
            foreach (var pair in _pages)
                PanelElements.SetVisible(pair.Value, pair.Key == key);
        }

        public void Refresh()
        {
        }

        public void Capture(EditorPrefs prefs, WorldViewState world) => prefs.SettingsTab = _tabs.Current;

        public void Restore(EditorPrefs prefs, WorldViewState world)
        {
            if (prefs.SettingsTab == null || !_pages.ContainsKey(prefs.SettingsTab))
                return;
            _tabs.Select(prefs.SettingsTab);
            ShowPage(prefs.SettingsTab);
        }
    }
}
