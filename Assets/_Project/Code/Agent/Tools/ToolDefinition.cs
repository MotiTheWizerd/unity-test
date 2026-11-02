using System;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Tools
{
    /// <summary>A tool as a model sees it: its name, what it does, and the JSON schema of its input.</summary>
    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, string description, JObject inputSchema)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            InputSchema = inputSchema ?? throw new ArgumentNullException(nameof(inputSchema));
        }

        public string Name { get; }

        public string Description { get; }

        public JObject InputSchema { get; }
    }
}
