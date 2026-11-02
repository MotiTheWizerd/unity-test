namespace Forge.Agent.Tools
{
    /// <summary>
    /// What a tool call returned: <see cref="Content"/> for the model (the result as JSON, or why it failed) and a
    /// one-line <see cref="Summary"/> for people.
    /// </summary>
    public sealed class ToolResult
    {
        ToolResult(string content, string summary, bool isError)
        {
            Content = content;
            Summary = summary;
            IsError = isError;
        }

        public static ToolResult Success(string content, string summary) => new ToolResult(content, summary, false);

        /// <summary>A refused or failed call; the model reads <paramref name="reason"/> and can try again.</summary>
        public static ToolResult Failure(string reason) => new ToolResult(reason, reason, true);

        public string Content { get; }

        public string Summary { get; }

        public bool IsError { get; }
    }
}
