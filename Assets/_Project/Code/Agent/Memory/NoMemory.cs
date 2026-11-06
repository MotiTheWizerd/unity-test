using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Forge.Agent.Memory
{
    /// <summary>The agent without a memory: nothing is recalled and nothing is kept.</summary>
    public sealed class NoMemory : IAgentMemory
    {
        public static readonly NoMemory Instance = new NoMemory();

        NoMemory()
        {
        }

        public Task<MemoryDress> DressAsync(string cue, CancellationToken cancellation) => Task.FromResult(MemoryDress.None);

        public Task<MemorySleep> SleepAsync(IReadOnlyList<MemoryCall> calls, IReadOnlyList<string> recalled, string runId, CancellationToken cancellation) =>
            Task.FromResult(MemorySleep.Skip("the agent keeps no memory"));
    }
}
