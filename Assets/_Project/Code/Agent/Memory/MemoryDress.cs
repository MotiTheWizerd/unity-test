using System;
using System.Collections.Generic;

namespace Forge.Agent.Memory
{
    /// <summary>What a turn wears from the memory: the block to put in front of the message, and what it recalled.</summary>
    public sealed class MemoryDress
    {
        public static readonly MemoryDress None = new MemoryDress(string.Empty, Array.Empty<string>());

        public MemoryDress(string block, IReadOnlyList<string> recalled)
        {
            Block = block ?? string.Empty;
            Recalled = recalled ?? Array.Empty<string>();
        }

        /// <summary>The standing orders, a note on how the memory works and the recalled memories; empty for none.</summary>
        public string Block { get; }

        /// <summary>Names of the memories the block carries.</summary>
        public IReadOnlyList<string> Recalled { get; }
    }
}
