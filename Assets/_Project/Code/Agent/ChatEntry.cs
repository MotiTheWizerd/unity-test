namespace Forge.Agent
{
    /// <summary>One line of the agent's transcript.</summary>
    public sealed class ChatEntry
    {
        public ChatEntry(ChatRole role, string text)
        {
            Role = role;
            Text = text ?? string.Empty;
        }

        public ChatRole Role { get; }

        public string Text { get; }
    }
}
