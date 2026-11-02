using System;
using Forge.Agent;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The agent dock: the transcript, the message box (Enter sends, Shift+Enter starts a new line), Send, Stop and
    /// New chat, and, while a build waits for review, Accept, Retry and Discard. New transcript lines are appended
    /// as the conversation grows; the whole transcript is only rebuilt when it was cleared.
    /// </summary>
    public sealed class AgentSection : IPanelSection
    {
        const string EntryClass = "forge-chat";

        readonly AgentConversation _conversation;
        readonly ScrollView _transcript;
        readonly VisualElement _review;
        readonly Label _reviewSummary;
        readonly Label _status;
        readonly TextField _input;
        readonly Button _send;
        readonly Button _stop;
        readonly Button _restart;
        int _shownRevision = -1;
        int _shownEntries;
        AgentState _shownState;
        float _workingSince;

        public AgentSection(VisualElement root, AgentConversation conversation)
        {
            _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
            _transcript = PanelElements.Required<ScrollView>(root, "agent-transcript");
            _review = PanelElements.Required<VisualElement>(root, "agent-review");
            _reviewSummary = PanelElements.Required<Label>(root, "agent-review-summary");
            _status = PanelElements.Required<Label>(root, "agent-status");
            _input = PanelElements.Required<TextField>(root, "agent-input");
            _send = PanelElements.Required<Button>(root, "agent-send");
            _stop = PanelElements.Required<Button>(root, "agent-stop");
            _restart = PanelElements.Required<Button>(root, "agent-new");

            _send.clicked += Send;
            _stop.clicked += conversation.Stop;
            _restart.clicked += conversation.Restart;
            PanelElements.Required<Button>(root, "agent-accept").clicked += conversation.Accept;
            PanelElements.Required<Button>(root, "agent-retry").clicked += conversation.Retry;
            PanelElements.Required<Button>(root, "agent-discard").clicked += conversation.Discard;
            _input.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        public void Refresh()
        {
            var state = _conversation.State;
            if (state == AgentState.Working && _shownState != AgentState.Working)
                _workingSince = Time.realtimeSinceStartup;
            _shownState = state;

            if (_conversation.Revision != _shownRevision)
            {
                _shownRevision = _conversation.Revision;
                ShowNewEntries();
            }

            var working = state == AgentState.Working;
            var reviewing = state == AgentState.Reviewing;
            _send.SetEnabled(!working);
            PanelElements.SetVisible(_stop, working);
            _restart.SetEnabled(state == AgentState.Idle && _conversation.Entries.Count > 0);

            PanelElements.SetVisible(_review, reviewing);
            if (reviewing)
                PanelElements.SetText(_reviewSummary,
                    $"Review the build ({_conversation.DraftSummary}). Accept keeps it as one undo step, Retry builds it again, " +
                    "Discard removes it. Or send a follow-up to change it.");

            PanelElements.SetVisible(_status, working);
            if (working)
                PanelElements.SetText(_status, $"Working… {Time.realtimeSinceStartup - _workingSince:0} s");
        }

        void Send()
        {
            var text = _input.value;
            if (string.IsNullOrWhiteSpace(text) || _conversation.State == AgentState.Working)
                return;

            _conversation.Send(text);
            _input.value = string.Empty;
        }

        // Enter sends, Shift+Enter starts a new line. One Enter arrives as two events (the key, then its newline
        // character); both are kept from the text field, and only the key sends.
        void OnKeyDown(KeyDownEvent e)
        {
            var enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.character == '\n';
            if (!enter || e.shiftKey)
                return;

            if (e.keyCode != KeyCode.None)
                Send();
            e.StopImmediatePropagation();
            _input.focusController?.IgnoreEvent(e);
        }

        void ShowNewEntries()
        {
            var entries = _conversation.Entries;
            if (entries.Count < _shownEntries)
            {
                _transcript.Clear();
                _shownEntries = 0;
            }

            Label last = null;
            for (var i = _shownEntries; i < entries.Count; i++)
            {
                last = new Label(entries[i].Text);
                last.AddToClassList(EntryClass);
                last.AddToClassList($"{EntryClass}--{entries[i].Role.ToString().ToLowerInvariant()}");
                last.selection.isSelectable = true;
                _transcript.Add(last);
            }

            _shownEntries = entries.Count;
            // Once laid out, the newest line comes into view.
            if (last != null)
            {
                var newest = last;
                _transcript.schedule.Execute(() => _transcript.ScrollTo(newest));
            }
        }
    }
}
