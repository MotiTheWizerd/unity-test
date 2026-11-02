using System;

namespace Forge.Agent
{
    /// <summary>One request for a backend to work through.</summary>
    public sealed class AgentTurn
    {
        public AgentTurn(string prompt, string instructions, string conversationId)
        {
            Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            Instructions = instructions ?? throw new ArgumentNullException(nameof(instructions));
            ConversationId = conversationId;
        }

        /// <summary>The user's message, with the editor's state in front of it.</summary>
        public string Prompt { get; }

        /// <summary>The agent's standing instructions (its system prompt).</summary>
        public string Instructions { get; }

        /// <summary>The backend's id of the conversation to continue, or null to start a new one.</summary>
        public string ConversationId { get; }
    }
}
