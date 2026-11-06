using System.Collections.Generic;

namespace Forge.Agent.Tools
{
    /// <summary>
    /// What a tool call returned: <see cref="Content"/> for the model (the result as JSON, or why it failed), any
    /// <see cref="Pictures"/> it brought back, and a one-line <see cref="Summary"/> for people.
    /// </summary>
    public sealed class ToolResult
    {
        ToolResult(string content, string summary, bool isError, IReadOnlyList<AgentImage> pictures)
        {
            Content = content;
            Summary = summary;
            IsError = isError;
            Pictures = pictures ?? AgentImage.None;
        }

        public static ToolResult Success(string content, string summary, IReadOnlyList<AgentImage> pictures = null) =>
            new ToolResult(content, summary, false, pictures);

        /// <summary>A refused or failed call; the model reads <paramref name="reason"/> and can try again.</summary>
        public static ToolResult Failure(string reason) => new ToolResult(reason, reason, true, null);

        public string Content { get; }

        public string Summary { get; }

        public bool IsError { get; }

        /// <summary>Pictures the model sees after <see cref="Content"/> (none for most tools).</summary>
        public IReadOnlyList<AgentImage> Pictures { get; }
    }
}
