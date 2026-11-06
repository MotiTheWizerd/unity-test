using System;
using System.Collections.Generic;

namespace Forge.Agent.Memory
{
    /// <summary>What a sleep did: how many memories it wrote, or why it wrote none.</summary>
    public sealed class MemorySleep
    {
        public MemorySleep(int created, int updated, IReadOnlyList<string> names, string skipped, string error)
        {
            Created = created;
            Updated = updated;
            Names = names ?? Array.Empty<string>();
            Skipped = string.IsNullOrWhiteSpace(skipped) ? null : skipped;
            Error = string.IsNullOrWhiteSpace(error) ? null : error;
        }

        public static MemorySleep Skip(string why) => new MemorySleep(0, 0, null, why, null);

        public static MemorySleep Fail(string why) => new MemorySleep(0, 0, null, null, why);

        public int Created { get; }

        public int Updated { get; }

        public IReadOnlyList<string> Names { get; }

        /// <summary>Why there was nothing to distill, or null.</summary>
        public string Skipped { get; }

        /// <summary>Why the sleep failed, or null.</summary>
        public string Error { get; }

        /// <summary>A line for the chat, or null when there is nothing worth telling the user.</summary>
        public string Describe()
        {
            if (Error != null)
                return $"Memory couldn't keep this: {Error}";
            var kept = Created + Updated;
            if (kept == 0)
                return null;
            return kept == 1 ? "Remembered 1 thing." : $"Remembered {kept} things.";
        }
    }
}
