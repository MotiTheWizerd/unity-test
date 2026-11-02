namespace Forge.Agent
{
    public enum AgentEventKind
    {
        /// <summary>The backend's id for the conversation, to continue it with the next request.</summary>
        ConversationStarted,

        /// <summary>Something the agent says to the user.</summary>
        Text,

        /// <summary>Something the user should know about how the agent is running.</summary>
        Notice,

        /// <summary>The request is done; <see cref="AgentEvent.Failed"/> says whether it went wrong.</summary>
        Finished,
    }
}
