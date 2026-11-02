namespace Forge.Agent
{
    /// <summary>Something that happened while a backend worked on a request.</summary>
    public sealed class AgentEvent
    {
        AgentEvent(AgentEventKind kind, string text, bool failed)
        {
            Kind = kind;
            Text = text;
            Failed = failed;
        }

        public static AgentEvent ConversationStarted(string conversationId) => new AgentEvent(AgentEventKind.ConversationStarted, conversationId, false);

        public static AgentEvent Said(string text) => new AgentEvent(AgentEventKind.Text, text, false);

        public static AgentEvent Notice(string text) => new AgentEvent(AgentEventKind.Notice, text, false);

        public static AgentEvent Done() => new AgentEvent(AgentEventKind.Finished, null, false);

        public static AgentEvent Failure(string reason) => new AgentEvent(AgentEventKind.Finished, reason, true);

        public AgentEventKind Kind { get; }

        /// <summary>The conversation id, the words, the notice, or why the request failed.</summary>
        public string Text { get; }

        /// <summary>For <see cref="AgentEventKind.Finished"/>: whether the request failed.</summary>
        public bool Failed { get; }
    }
}
