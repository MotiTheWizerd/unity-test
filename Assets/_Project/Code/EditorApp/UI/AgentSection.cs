using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Agent;
using Forge.EditorApp.Agent;
using Forge.EditorApp.Tools;
using Forge.EditorApp.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// The agent dock: the transcript, the message box (Enter sends, Shift+Enter starts a new line), Send, Stop and
    /// New chat, and, while a build waits for review, Accept, Retry and Discard. Pictures go with the next message:
    /// the camera button attaches what the view shows and Ctrl+V pastes one from the clipboard; they wait as
    /// thumbnails above the message box (× takes one off) and stay on the user's line once sent. New transcript lines
    /// are appended as the conversation grows; the whole transcript is only rebuilt when it was cleared.
    /// </summary>
    public sealed class AgentSection : IPanelSection
    {
        const string EntryClass = "forge-chat";
        const string PicturesClass = "forge-agent-pictures";
        const string PictureClass = "forge-agent-picture";
        const string RemovePictureClass = "forge-agent-picture__remove";
        // Enough to compare a few views; more would only slow the request.
        const int MaxPictures = 6;

        readonly AgentConversation _conversation;
        readonly EditorContext _context;
        readonly Func<AgentImage> _snapshot;
        readonly IImageClipboard _clipboard;
        readonly List<AgentImage> _pending = new List<AgentImage>();
        // The textures the thumbnails show, made once per picture and destroyed when it leaves the panel.
        readonly Dictionary<AgentImage, Texture2D> _thumbnails = new Dictionary<AgentImage, Texture2D>();
        readonly VisualElement _attachments;
        readonly Button _attachView;
        readonly ScrollView _transcript;
        readonly Label _markText;
        readonly Button _mark;
        readonly Button _clearMark;
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

        /// <param name="snapshot">Takes a picture of what the view shows.</param>
        public AgentSection(VisualElement root, AgentConversation conversation, EditorContext context, Func<AgentImage> snapshot, IImageClipboard clipboard)
        {
            _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            _attachments = PanelElements.Required<VisualElement>(root, "agent-attachments");
            _attachView = PanelElements.Required<Button>(root, "agent-snapshot");
            _attachView.clicked += AttachView;
            _markText = PanelElements.Required<Label>(root, "agent-mark-text");
            _mark = PanelElements.Required<Button>(root, "agent-mark");
            _clearMark = PanelElements.Required<Button>(root, "agent-mark-clear");
            _mark.clicked += () => _context.SelectTool(_context.ActiveTool == ToolKind.Mark ? ToolKind.Select : ToolKind.Mark);
            _clearMark.clicked += () => _context.Mark = null;
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
            _attachView.SetEnabled(_pending.Count < MaxPictures);
            PanelElements.SetVisible(_attachments, _pending.Count > 0);
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

            RefreshMark();
        }

        void RefreshMark()
        {
            var mark = _context.Mark;
            PanelElements.SetText(_markText, mark == null ? "No mark: \"here\" is where the camera looks." : $"Here: {mark.Describe()}");
            PanelElements.SetActive(_mark, _context.ActiveTool == ToolKind.Mark);
            _clearMark.SetEnabled(mark != null);
        }

        void Send()
        {
            var text = _input.value;
            if ((string.IsNullOrWhiteSpace(text) && _pending.Count == 0) || _conversation.State == AgentState.Working)
                return;

            _conversation.Send(text, _pending.ToList());
            _input.value = string.Empty;
            // Sent pictures keep their textures: the transcript shows them next.
            _pending.Clear();
            _attachments.Clear();
        }

        void AttachView()
        {
            if (_pending.Count >= MaxPictures)
                return;
            try
            {
                Attach(_snapshot());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Couldn't take a picture of the view for the agent: {ex.Message}");
            }
        }

        // Ctrl+V with a picture on the clipboard attaches it; with words there, the text box pastes them as always.
        bool TryPastePicture()
        {
            if (_pending.Count >= MaxPictures)
                return false;
            AgentImage picture;
            try
            {
                picture = _clipboard.ReadPicture();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Couldn't read the picture on the clipboard: {ex.Message}");
                return false;
            }

            if (picture == null)
                return false;
            Attach(picture);
            return true;
        }

        void Attach(AgentImage picture)
        {
            _pending.Add(picture);
            var thumbnail = Picture(picture);
            var remove = new ForgeButton(() =>
            {
                _pending.Remove(picture);
                thumbnail.RemoveFromHierarchy();
                Forget(picture);
            })
            {
                icon = "x",
                small = true,
                variant = ForgeButtonVariant.Ghost,
                tooltip = "Don't send this picture",
            };
            remove.AddToClassList(RemovePictureClass);
            thumbnail.Add(remove);
            _attachments.Add(thumbnail);
        }

        VisualElement Picture(AgentImage picture)
        {
            if (!_thumbnails.TryGetValue(picture, out var texture))
            {
                texture = AgentPictures.Thumbnail(picture);
                _thumbnails.Add(picture, texture);
            }

            var element = new VisualElement { style = { backgroundImage = texture } };
            element.AddToClassList(PictureClass);
            return element;
        }

        void Forget(AgentImage picture)
        {
            if (_thumbnails.TryGetValue(picture, out var texture))
                UnityEngine.Object.Destroy(texture);
            _thumbnails.Remove(picture);
        }

        // Enter sends, Shift+Enter starts a new line. One Enter arrives as two events (the key, then its newline
        // character); both are kept from the text field, and only the key sends.
        void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.V && e.actionKey && !e.shiftKey && !e.altKey && TryPastePicture())
            {
                e.StopImmediatePropagation();
                _input.focusController?.IgnoreEvent(e);
                return;
            }

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
                foreach (var shown in _thumbnails.Keys.Except(_pending).ToList())
                    Forget(shown);
            }

            VisualElement last = null;
            for (var i = _shownEntries; i < entries.Count; i++)
            {
                last = Entry(entries[i]);
                last.AddToClassList(EntryClass);
                last.AddToClassList($"{EntryClass}--{entries[i].Role.ToString().ToLowerInvariant()}");
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

        // The agent writes Markdown; everything else (the user's words, tool activity, notices) is shown as written,
        // under the pictures the user sent with it.
        VisualElement Entry(ChatEntry entry)
        {
            if (entry.Role == ChatRole.Agent)
                return MarkdownView.Build(entry.Text);

            var label = new Label(entry.Text) { enableRichText = false };
            label.selection.isSelectable = true;
            if (entry.Images.Count == 0)
                return label;

            var line = new VisualElement();
            var pictures = new VisualElement();
            pictures.AddToClassList(PicturesClass);
            foreach (var picture in entry.Images)
                pictures.Add(Picture(picture));
            line.Add(pictures);
            if (entry.Text.Length > 0)
                line.Add(label);
            return line;
        }
    }
}
