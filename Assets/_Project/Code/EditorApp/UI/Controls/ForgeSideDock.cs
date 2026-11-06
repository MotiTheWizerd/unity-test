using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A dock along a side of the screen: a glass card (<see cref="ForgePanel"/>'s look) with a row of tabs over its
    /// pages, one shown at a time. Each tab is a small icon button, as in the top bar (the page's <see cref="ForgeDockPage.icon"/>,
    /// its title as the tooltip; a page without an icon shows its title), lit while its page is on show, with a vertical
    /// <see cref="ForgeSeparator"/> between tabs. The pages are its <see cref="ForgeDockPage"/> children in UXML, in order,
    /// and the tabs are made from them when the dock joins a panel (<see cref="RebuildTabs"/> after adding pages from code).
    /// Where it stands is the layout's business (classes on it); it only opens, closes and turns pages. Styled by
    /// ForgeSideDock.uss.
    /// </summary>
    [UxmlElement]
    public partial class ForgeSideDock : VisualElement
    {
        public const string Class = "forge-side-dock";
        public const string HeaderClass = "forge-side-dock__header";
        public const string PagesClass = "forge-side-dock__pages";
        public const string TabClass = "forge-side-dock__tab";

        readonly VisualElement _header;
        readonly VisualElement _pages;
        readonly Dictionary<string, ForgeButton> _tabs = new Dictionary<string, ForgeButton>(StringComparer.Ordinal);

        public ForgeSideDock()
        {
            AddToClassList(ForgePanel.Class);
            AddToClassList(Class);

            _header = new VisualElement { name = "header" };
            _header.AddToClassList(HeaderClass);
            hierarchy.Add(_header);

            _pages = new VisualElement { name = "pages" };
            _pages.AddToClassList(PagesClass);
            hierarchy.Add(_pages);

            RegisterCallback<AttachToPanelEvent>(_ => RebuildTabs());
        }

        public override VisualElement contentContainer => _pages;

        /// <summary>The pages, in order.</summary>
        public IEnumerable<ForgeDockPage> Pages => _pages.Children().OfType<ForgeDockPage>();

        /// <summary>The key of the page on show; null while there are no pages.</summary>
        public string Current { get; private set; }

        public bool IsOpen => !ClassListContains(PanelElements.HiddenClass);

        /// <summary>Raised when the dock opens (true) or closes (false).</summary>
        public event Action<bool> OpenChanged;

        /// <summary>Raised when another page is shown, by the user or from code.</summary>
        public event Action<string> PageChanged;

        /// <summary>Makes a tab per page, keeping the page on show if it is still there.</summary>
        public void RebuildTabs()
        {
            _header.Clear();
            _tabs.Clear();
            foreach (var page in Pages)
            {
                if (_tabs.ContainsKey(page.key))
                    throw new InvalidOperationException($"Dock page key '{page.key}' is used twice.");
                if (_tabs.Count > 0)
                    _header.Add(new ForgeSeparator { vertical = true });

                var key = page.key;
                var tab = new ForgeButton { small = true, tooltip = page.title };
                if (string.IsNullOrEmpty(page.icon))
                    tab.text = page.title;
                else
                    tab.icon = page.icon;
                tab.AddToClassList(TabClass);
                tab.clicked += () => Show(key);
                _header.Add(tab);
                _tabs.Add(key, tab);
            }

            ShowPage(HasPage(Current) ? Current : Pages.Select(page => page.key).FirstOrDefault());
        }

        public bool HasPage(string key) => key != null && _tabs.ContainsKey(key);

        /// <summary>The page <paramref name="key"/>, or null.</summary>
        public ForgeDockPage Page(string key) => Pages.FirstOrDefault(page => page.key == key);

        public void SetOpen(bool open)
        {
            if (open == IsOpen)
                return;
            PanelElements.SetVisible(this, open);
            OpenChanged?.Invoke(open);
        }

        /// <summary>Shows page <paramref name="key"/> (the dock stays as open or closed as it was); false if there is none.</summary>
        public bool Show(string key)
        {
            if (!HasPage(key))
                return false;
            if (key == Current)
                return true;
            ShowPage(key);
            PageChanged?.Invoke(key);
            return true;
        }

        /// <summary>
        /// What a button for page <paramref name="key"/> does: opens the dock on it, or turns to it if another page is on
        /// show, or closes the dock if it is already showing it.
        /// </summary>
        public void Toggle(string key)
        {
            if (IsOpen && key == Current)
            {
                SetOpen(false);
                return;
            }

            Show(key);
            SetOpen(true);
        }

        void ShowPage(string key)
        {
            Current = key;
            foreach (var page in Pages)
                PanelElements.SetVisible(page, page.key == key);
            foreach (var pair in _tabs)
                PanelElements.SetActive(pair.Value, pair.Key == key);
        }
    }
}
