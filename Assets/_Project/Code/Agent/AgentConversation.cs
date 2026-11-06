using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Forge.Agent.Memory;
using Forge.Agent.Tools;
using Forge.Editing;
using Forge.Editing.History;

namespace Forge.Agent
{
    /// <summary>
    /// The chat with the world-building agent, and what it builds. Each request runs on the backend while its
    /// edits collect in one <see cref="EditDraft"/>; the user then accepts it (one undo step), retries or discards
    /// it, and a follow-up message refines the same draft. Every turn is dressed with what the agent's memory
    /// recalls for it, and once a request settles (accepted, discarded, or an answer with nothing to review) its
    /// turns and the user's verdict are slept into the memory. Call every member on the main thread: backend
    /// events and tool calls are brought there through the <see cref="MainThreadQueue"/>.
    /// </summary>
    public sealed class AgentConversation : IDisposable
    {
        const int UndoNameLength = 48;

        readonly IAgentBackend _backend;
        readonly AgentToolHost _tools;
        readonly EditSession _session;
        readonly MainThreadQueue _mainThread;
        readonly Func<string> _briefing;
        readonly string _instructions;
        readonly IAgentMemory _memory;
        readonly List<ChatEntry> _entries = new List<ChatEntry>();
        readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        // The request's turns and what they were dressed with, kept for its sleep.
        readonly List<MemoryCall> _calls = new List<MemoryCall>();
        readonly List<string> _recalled = new List<string>();
        CancellationTokenSource _turn;
        TurnNotes _notes;
        string _draftRequest;
        string _conversationId;
        string _runId;
        bool _memoryWarned;
        bool _disposed;

        /// <param name="briefing">The editor's state (where the camera looks, what is selected), put in front of each message.</param>
        /// <param name="instructions">The agent's standing instructions (its system prompt).</param>
        /// <param name="memory">The agent's long-term memory (<see cref="NoMemory.Instance"/> for none).</param>
        public AgentConversation(IAgentBackend backend, AgentToolHost tools, EditSession session, MainThreadQueue mainThread,
            Func<string> briefing, string instructions, IAgentMemory memory)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
            _briefing = briefing ?? throw new ArgumentNullException(nameof(briefing));
            _instructions = instructions ?? throw new ArgumentNullException(nameof(instructions));
            _memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _tools.Called += OnToolCalled;
        }

        public AgentState State { get; private set; }

        public IReadOnlyList<ChatEntry> Entries => _entries;

        /// <summary>Goes up whenever the transcript or the state changes, so a view knows when to redraw.</summary>
        public int Revision { get; private set; }

        /// <summary>The build in progress or waiting for review, or null.</summary>
        public EditDraft Draft { get; private set; }

        /// <summary>What the draft does to the world, e.g. "12 added, 2 removed".</summary>
        public string DraftSummary
        {
            get
            {
                if (Draft == null)
                    return string.Empty;

                int added = 0, changed = 0, removed = 0;
                foreach (var change in Draft.Changes)
                {
                    if (change.After.Kind != EntryKind.Written)
                        removed++;
                    else if (change.Before.Kind == EntryKind.Absent && change.Id.Namespace == _session.ActivePluginId)
                        added++;
                    else
                        changed++;
                }

                var parts = new List<string>(3);
                if (added > 0)
                    parts.Add($"{added} added");
                if (changed > 0)
                    parts.Add($"{changed} changed");
                if (removed > 0)
                    parts.Add($"{removed} removed");
                return parts.Count > 0 ? string.Join(", ", parts) : "no changes";
            }
        }

        public void Send(string message)
        {
            var text = message?.Trim();
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("The message is empty.", nameof(message));
            if (State == AgentState.Working)
                throw new InvalidOperationException("The agent is still working on the last message.");

            Add(ChatRole.User, text);
            Start(text, text);
        }

        /// <summary>Stops the request being worked on; what was built so far stays for review.</summary>
        public void Stop()
        {
            if (State == AgentState.Working)
                _turn.Cancel();
        }

        /// <summary>Keeps the build as one undo step named after the request.</summary>
        public void Accept()
        {
            EnsureReviewing("accept");
            var summary = DraftSummary;
            Draft.Accept(UndoName(_draftRequest));
            EndDraft();
            Add(ChatRole.Notice, "Accepted as one undo step.");
            Sleep($"I accepted what you built ({summary}) and kept it.");
            Changed();
        }

        public void Discard()
        {
            EnsureReviewing("discard");
            Draft.Discard();
            EndDraft();
            Add(ChatRole.Notice, "Discarded.");
            Sleep("I discarded what you built.");
            Changed();
        }

        /// <summary>Discards the build and asks for it again; the agent is told the first try was turned down.</summary>
        public void Retry()
        {
            EnsureReviewing("retry");
            var request = _draftRequest;
            Draft.Discard();
            EndDraft();
            Add(ChatRole.Notice, "Discarded; asking again.");
            Start($"I discarded what you built. Build it again from scratch, in a different way. My request was:\n{request}", request);
        }

        /// <summary>Forgets the conversation, so the next message starts afresh. The world stays as it is.</summary>
        public void Restart()
        {
            if (State != AgentState.Idle)
                throw new InvalidOperationException("Finish the current request first.");

            _conversationId = null;
            _entries.Clear();
            Changed();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _tools.Called -= OnToolCalled;
            _tools.Close();
            _turn?.Cancel();
            // A request still waiting for review when the editor closes is not slept, and a sleep in flight stops.
            _lifetime.Cancel();
            _backend.Dispose();
        }

        void Start(string prompt, string request)
        {
            if (Draft == null)
            {
                Draft = _session.BeginDraft();
                _draftRequest = request;
            }

            _runId = _runId ?? "forge-" + Guid.NewGuid().ToString("N");
            _notes = new TurnNotes(prompt);
            _turn = new CancellationTokenSource();
            var cancellation = _turn.Token;
            // Read here, on the main thread; the memory is dressed on the way to the backend.
            var briefed = WithBriefing(prompt);
            var conversationId = _conversationId;
            _tools.Open();
            State = AgentState.Working;
            Changed();

            Task.Run(() => RunTurnAsync(prompt, briefed, conversationId, cancellation), cancellation)
                .ContinueWith(task => _mainThread.Post(() => OnTurnEnded(task)), TaskScheduler.Default);
        }

        async Task RunTurnAsync(string cue, string briefed, string conversationId, CancellationToken cancellation)
        {
            var dress = await DressAsync(cue, cancellation).ConfigureAwait(false);
            var turn = new AgentTurn(WithMemory(dress.Block, briefed), _instructions, conversationId);
            await _backend.RunAsync(turn, happened => _mainThread.Post(() => OnEvent(happened)), cancellation).ConfigureAwait(false);
        }

        // A memory that can't be reached never stops the turn: it runs without, and the user is told once.
        async Task<MemoryDress> DressAsync(string cue, CancellationToken cancellation)
        {
            try
            {
                var dress = await _memory.DressAsync(cue, cancellation).ConfigureAwait(false);
                _mainThread.Post(() => OnDressed(dress, null));
                return dress;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _mainThread.Post(() => OnDressed(null, ex));
                return MemoryDress.None;
            }
        }

        string WithBriefing(string prompt)
        {
            var briefing = _briefing();
            return string.IsNullOrWhiteSpace(briefing) ? prompt : $"<editor>\n{briefing.Trim()}\n</editor>\n\n{prompt}";
        }

        // The memory rides in the message rather than the system prompt, so a resumed conversation keeps its cache.
        static string WithMemory(string block, string message) =>
            string.IsNullOrWhiteSpace(block) ? message : $"{block.Trim()}\n\n{message}";

        void OnDressed(MemoryDress dress, Exception failure)
        {
            if (_disposed)
                return;

            if (failure != null)
            {
                if (!_memoryWarned)
                    Add(ChatRole.Notice, $"Working without memory: {failure.GetBaseException().Message}");
                _memoryWarned = true;
                return;
            }

            _memoryWarned = false;
            foreach (var name in dress.Recalled)
            {
                if (!_recalled.Contains(name))
                    _recalled.Add(name);
            }
        }

        /// <summary>Hands the settled request to the memory, with <paramref name="verdict"/> (what the user did with the build) last.</summary>
        void Sleep(string verdict = null)
        {
            if (verdict != null)
                _calls.Add(new MemoryCall(verdict, string.Empty, null));
            var calls = _calls.ToList();
            var recalled = _recalled.ToList();
            var runId = _runId;
            _calls.Clear();
            _recalled.Clear();
            _runId = null;

            // Only a prompt, nothing said or done (a failure before the first word): nothing to learn from.
            if (!calls.Any(call => call.Answer.Length > 0 || call.ToolTrail.Count > 0))
                return;

            var lifetime = _lifetime.Token;
            Task.Run(() => _memory.SleepAsync(calls, recalled, runId, lifetime), lifetime)
                .ContinueWith(task => _mainThread.Post(() => OnSlept(task)), TaskScheduler.Default);
        }

        void OnSlept(Task<MemorySleep> sleep)
        {
            if (_disposed || sleep.IsCanceled)
                return;

            var line = sleep.IsFaulted
                ? $"Memory couldn't keep this: {sleep.Exception?.GetBaseException().Message}"
                : sleep.Result.Describe();
            if (line != null)
                Add(ChatRole.Notice, line);
        }

        void OnEvent(AgentEvent happened)
        {
            if (_disposed)
                return;

            switch (happened.Kind)
            {
                case AgentEventKind.ConversationStarted:
                    _conversationId = happened.Text;
                    break;
                case AgentEventKind.Text:
                    _notes?.Said(happened.Text);
                    Add(ChatRole.Agent, happened.Text);
                    break;
                case AgentEventKind.Notice:
                    Add(ChatRole.Notice, happened.Text);
                    break;
                case AgentEventKind.Finished:
                    if (happened.Failed)
                        Add(ChatRole.Error, happened.Text);
                    break;
            }
        }

        void OnToolCalled(ToolCall call)
        {
            if (_disposed)
                return;
            _notes?.Used(call);
            Add(ChatRole.Activity, call.Result.IsError ? $"{call.Tool} refused: {call.Result.Summary}" : call.Result.Summary);
        }

        void OnTurnEnded(Task turn)
        {
            if (_disposed)
                return;

            _tools.Close();
            _turn.Dispose();
            _turn = null;
            _calls.Add(_notes.ToCall());
            _notes = null;
            if (turn.IsCanceled)
                Add(ChatRole.Notice, "Stopped.");
            else if (turn.IsFaulted)
                Add(ChatRole.Error, $"The agent failed: {turn.Exception?.GetBaseException().Message}");

            // Nothing was built (an answer, or a failure before any change), so there is nothing to review.
            if (Draft != null && !Draft.HasChanges)
            {
                Draft.Discard();
                EndDraft();
            }

            State = Draft != null ? AgentState.Reviewing : AgentState.Idle;
            // A build waits for the user's verdict; anything else is settled now.
            if (State == AgentState.Idle)
                Sleep();
            Changed();
        }

        void EndDraft()
        {
            Draft = null;
            _draftRequest = null;
            State = AgentState.Idle;
        }

        void EnsureReviewing(string action)
        {
            if (State != AgentState.Reviewing)
                throw new InvalidOperationException($"There is no build to {action}.");
        }

        void Add(ChatRole role, string text)
        {
            _entries.Add(new ChatEntry(role, text));
            Changed();
        }

        void Changed() => Revision++;

        static string UndoName(string request)
        {
            var line = request.Split('\n')[0].Trim();
            return "Agent: " + (line.Length <= UndoNameLength ? line : line.Substring(0, UndoNameLength).TrimEnd() + "…");
        }

        /// <summary>One turn as its sleep reads it: the prompt without the briefing, the words, a line per tool call.</summary>
        sealed class TurnNotes
        {
            readonly string _prompt;
            readonly StringBuilder _answer = new StringBuilder();
            readonly List<string> _trail = new List<string>();

            public TurnNotes(string prompt) => _prompt = prompt;

            public void Said(string text)
            {
                if (_answer.Length > 0)
                    _answer.Append("\n\n");
                _answer.Append(text);
            }

            public void Used(ToolCall call) =>
                _trail.Add(call.Result.IsError ? $"← {call.Tool} refused: {call.Result.Summary}" : $"← {call.Tool}: {call.Result.Summary}");

            public MemoryCall ToCall() => new MemoryCall(_prompt, _answer.ToString(), _trail.ToList());
        }
    }
}
