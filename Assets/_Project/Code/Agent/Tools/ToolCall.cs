namespace Forge.Agent.Tools
{
    /// <summary>A tool call that has run: which tool, and what came of it.</summary>
    public sealed class ToolCall
    {
        public ToolCall(string tool, ToolResult result)
        {
            Tool = tool;
            Result = result;
        }

        public string Tool { get; }

        public ToolResult Result { get; }
    }
}
