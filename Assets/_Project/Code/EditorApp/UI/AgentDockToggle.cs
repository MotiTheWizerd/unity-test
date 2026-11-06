using System;
using Forge.Agent;
using Forge.EditorApp.UI.Controls;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The agent button at the end of the top bar's global row: opens the side dock on its Agent page, turns to that page
    /// if another is on show, and closes the dock if it already shows it (<see cref="ForgeSideDock.Toggle"/>). It is lit
    /// while the agent's page is on show, and pulses while the agent works, so a closed dock still says so.
    /// </summary>
    public sealed class AgentDockToggle : IPanelSection
    {
        readonly AgentConversation _conversation;
        readonly ForgeSideDock _dock;
        readonly ForgeButton _button;

        public AgentDockToggle(VisualElement root, ForgeSideDock dock, AgentConversation conversation)
        {
            _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
            _dock = dock ?? throw new ArgumentNullException(nameof(dock));
            _button = PanelElements.Required<ForgeButton>(root, "agent-toggle");
            _button.clicked += () => _dock.Toggle(SideDockSection.AgentPage);
        }

        public void Refresh()
        {
            PanelElements.SetActive(_button, _dock.IsOpen && _dock.Current == SideDockSection.AgentPage);
            _button.busy = _conversation.State == AgentState.Working;
        }
    }
}
