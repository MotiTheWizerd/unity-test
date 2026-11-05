using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI.Controls
{
    /// <summary>
    /// A row of tabs, one of them current. Reusable: the assets browser picks a category with it; any later
    /// panel with pages can too. Styled by TabBar.uss.
    /// </summary>
    public sealed class TabBar : VisualElement
    {
        public const string Class = "forge-tabs";
        public const string TabClass = "forge-tab";
        public const string DimmedClass = "is-dimmed";

        readonly Dictionary<string, Button> _tabs = new Dictionary<string, Button>(StringComparer.Ordinal);
        readonly List<string> _order = new List<string>();

        public TabBar()
        {
            AddToClassList(Class);
        }

        /// <summary>The current tab's key; null while there are no tabs.</summary>
        public string Current { get; private set; }

        /// <summary>Raised when the user picks another tab (not when <see cref="SetTabs"/> keeps or moves the current one).</summary>
        public event Action<string> Picked;

        /// <summary>
        /// Replaces the tabs. The current tab stays current if its key is still there; otherwise the first tab is.
        /// </summary>
        public void SetTabs(IReadOnlyList<(string Key, string Title)> tabs)
        {
            if (tabs == null) throw new ArgumentNullException(nameof(tabs));
            Clear();
            _tabs.Clear();
            _order.Clear();
            foreach (var (key, title) in tabs)
            {
                if (_tabs.ContainsKey(key))
                    throw new ArgumentException($"Tab key '{key}' is listed twice.", nameof(tabs));
                var tab = new Button { text = title };
                tab.AddToClassList(TabClass);
                tab.clicked += () => Pick(key);
                Add(tab);
                _tabs.Add(key, tab);
                _order.Add(key);
            }

            Current = Current != null && _tabs.ContainsKey(Current) ? Current : (_order.Count > 0 ? _order[0] : null);
            Highlight();
        }

        /// <summary>Makes <paramref name="key"/> current without raising <see cref="Picked"/>.</summary>
        public void Select(string key)
        {
            if (!_tabs.ContainsKey(key))
                throw new ArgumentException($"No tab '{key}'.", nameof(key));
            Current = key;
            Highlight();
        }

        /// <summary>Fades the tabs while they don't apply, e.g. during a search that spans all of them.</summary>
        public void SetDimmed(bool dimmed) => EnableInClassList(DimmedClass, dimmed);

        void Pick(string key)
        {
            if (key == Current)
                return;
            Current = key;
            Highlight();
            Picked?.Invoke(key);
        }

        void Highlight()
        {
            foreach (var pair in _tabs)
                PanelElements.SetActive(pair.Value, pair.Key == Current);
        }
    }
}
