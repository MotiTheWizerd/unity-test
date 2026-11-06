using System;
using Forge.Agent;
using Forge.EditorApp.UI.Controls;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The agent button at the end of the top bar's global row: shows and hides the agent dock on the right, and
    /// pulses while the agent works (so a closed dock still says so). The dock starts closed; the button is lit while
    /// it is open, and the layout root wears <see cref="OpenClass"/> so the top bar and the assets browser leave the
    /// dock its room (EditorPanel.uss, AssetsBrowser.uss).
    /// </summary>
    public sealed class AgentDockToggle : IPanelSection
    {
        public const string OpenClass = "is-agent-open";

        readonly AgentConversation _conversation;
        readonly ForgeButton _button;
        readonly VisualElement _dock;
        readonly VisualElement _layout;

        public AgentDockToggle(VisualElement root, AgentConversation conversation)
        {
            _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
            _button = PanelElements.Required<ForgeButton>(root, "agent-toggle");
            _dock = PanelElements.Required<VisualElement>(root, "agent-dock");
            _layout = PanelElements.Required<VisualElement>(root, "root");
            _button.clicked += () => SetOpen(!IsOpen);
            SetOpen(false);
        }

        public bool IsOpen => !_dock.ClassListContains(PanelElements.HiddenClass);

        public void SetOpen(bool open)
        {
            PanelElements.SetVisible(_dock, open);
            PanelElements.SetActive(_button, open);
            _layout.EnableInClassList(OpenClass, open);
        }

        public void Refresh() => _button.busy = _conversation.State == AgentState.Working;
    }
}
