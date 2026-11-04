using System;
using System.IO;
using System.Linq;
using Forge.Agent;
using Forge.Agent.ClaudeCode;
using Forge.Agent.Tools;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using Forge.Editing.History;
using Forge.Editing.Placement;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// The world-building agent inside the running editor: its commands, over the same seating and placement
    /// options as the place tool; the Claude Code backend; and the conversation the panel shows. Ticked every
    /// frame to run the agent's work on the main thread. While it works the app keeps running in the background,
    /// so a build carries on when the window loses focus.
    /// </summary>
    public sealed class AgentHost : IDisposable
    {
        /// <summary>Folder under the app's data folder where Claude Code runs and keeps the agent's conversations.</summary>
        const string WorkingFolder = "Agent";

        readonly EditorContext _context;
        readonly MainThreadQueue _mainThread = new MainThreadQueue();
        readonly bool _runInBackground;
        EditDraft _shownDraft;
        int _shownRevision;

        public AgentHost(EditorContext context, ObjectSeating seating, EditorBriefing briefing, AgentSettings settings)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (seating == null)
                throw new ArgumentNullException(nameof(seating));
            if (briefing == null)
                throw new ArgumentNullException(nameof(briefing));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var commands = new CommandRegistry();
            BuildingCommands.AddTo(commands, new BuildContext(context.Session, () => context.ActiveSpace, seating, context.PlacementOptions));
            var tools = new AgentToolHost(new ToolInvoker(commands), _mainThread);
            var backend = new ClaudeCodeBackend(settings.ClaudeCode(Path.Combine(Application.persistentDataPath, WorkingFolder)), tools);
            backend.EndpointFailed += ex => _mainThread.Post(() => Debug.LogException(ex));
            Conversation = new AgentConversation(backend, tools, context.Session, _mainThread, briefing.Describe, settings.Instructions);
            _runInBackground = Application.runInBackground;
        }

        public AgentConversation Conversation { get; }

        /// <summary>Runs the agent's work that is waiting for the main thread; call once per frame.</summary>
        public void Tick()
        {
            _mainThread.Pump();
            var background = _runInBackground || Conversation.State == AgentState.Working;
            if (Application.runInBackground != background)
                Application.runInBackground = background;
            SelectDraft();
        }

        public void Dispose()
        {
            Conversation.Dispose();
            Application.runInBackground = _runInBackground;
        }

        // While the draft is open, the objects it places or changes are the selection: they stand out, and F frames them.
        void SelectDraft()
        {
            var draft = Conversation.Draft;
            if (draft == null || (ReferenceEquals(draft, _shownDraft) && draft.Revision == _shownRevision))
                return;

            _shownDraft = draft;
            _shownRevision = draft.Revision;
            _context.Selection.Replace(draft.Changes
                .Where(change => change.After.Kind == EntryKind.Written && change.After.Record is PlacedObjectRecord)
                .Select(change => change.Id));
        }
    }
}
