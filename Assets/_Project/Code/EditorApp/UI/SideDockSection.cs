using Forge.EditorApp.UI.Controls;
using Forge.Editing.State;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The side dock on the right (<see cref="ForgeSideDock"/>): an Agent page (the chat, run by <see cref="AgentSection"/>)
    /// and a Materials page (<see cref="MaterialsPage"/>). It starts closed; buttons open it on their page
    /// (<see cref="DockPageToggle"/>: the top bar's agent button, the assets browser's materials button). While it is
    /// open the layout root wears <see cref="OpenClass"/>, so the assets browser leaves it its room (EditorPanel.uss). Its
    /// open state and page are kept between sessions.
    /// </summary>
    public sealed class SideDockSection : IPanelSection, IKeptSection
    {
        public const string OpenClass = "is-side-dock-open";
        public const string AgentPage = "agent";
        public const string MaterialsPage = "materials";

        readonly VisualElement _layout;

        public SideDockSection(VisualElement root)
        {
            Dock = PanelElements.Required<ForgeSideDock>(root, "side-dock");
            _layout = PanelElements.Required<VisualElement>(root, "root");
            Dock.OpenChanged += open => _layout.EnableInClassList(OpenClass, open);
            _layout.EnableInClassList(OpenClass, Dock.IsOpen);
        }

        public ForgeSideDock Dock { get; }

        /// <summary>Takes page <paramref name="key"/> and its tab out of the dock (a feature that isn't there, such as the agent).</summary>
        public void RemovePage(string key)
        {
            var page = Dock.Page(key);
            if (page == null)
                return;
            page.RemoveFromHierarchy();
            Dock.RebuildTabs();
        }

        public void Refresh()
        {
        }

        public void Capture(EditorPrefs prefs, WorldViewState world)
        {
            prefs.SideDockOpen = Dock.IsOpen;
            prefs.SideDockPage = Dock.Current;
        }

        public void Restore(EditorPrefs prefs, WorldViewState world)
        {
            if (prefs.SideDockPage != null)
                Dock.Show(prefs.SideDockPage);
            if (prefs.SideDockOpen is bool open)
                Dock.SetOpen(open);
        }
    }
}
