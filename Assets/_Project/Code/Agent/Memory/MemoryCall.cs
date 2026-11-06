using System;
using System.Collections.Generic;

namespace Forge.Agent.Memory
{
    /// <summary>One turn as the memory reads it: what the agent was told, what it answered, and the tools it used.</summary>
    public sealed class MemoryCall
    {
        public MemoryCall(string prompt, string answer, IReadOnlyList<string> toolTrail)
        {
            Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            Answer = answer ?? string.Empty;
            ToolTrail = toolTrail ?? Array.Empty<string>();
        }

        public string Prompt { get; }

        public string Answer { get; }

        /// <summary>One line per tool call, e.g. "← place_objects: Placed 3 objects".</summary>
        public IReadOnlyList<string> ToolTrail { get; }
    }
}
