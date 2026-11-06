using System.Collections.Generic;

namespace Forge.Agent
{
    /// <summary>One line of the agent's transcript.</summary>
    public sealed class ChatEntry
    {
        public ChatEntry(ChatRole role, string text, IReadOnlyList<AgentImage> images = null)
        {
            Role = role;
            Text = text ?? string.Empty;
            Images = images ?? AgentImage.None;
        }

        public ChatRole Role { get; }

        public string Text { get; }

        /// <summary>The pictures the user sent with this message (none for every other line).</summary>
        public IReadOnlyList<AgentImage> Images { get; }
    }
}
