using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Forge.Agent.Memory
{
    /// <summary>
    /// The agent's long-term memory. Before each turn it is <b>dressed</b>: memories recalled for the message come back
    /// as a block the turn carries in front of the message, so the agent knows them rather than looks them up. After a
    /// request settles it <b>sleeps</b>: the request's turns are distilled into memories for later turns.
    /// </summary>
    public interface IAgentMemory
    {
        /// <summary>The block for a turn cued by <paramref name="cue"/>. Throws when the memory can't be reached.</summary>
        Task<MemoryDress> DressAsync(string cue, CancellationToken cancellation);

        /// <summary>
        /// Distills <paramref name="calls"/> (one request, in order) into the memory. <paramref name="recalled"/> names
        /// the memories the request was already dressed with, so they are not written again. Failures come back in the
        /// result, not as exceptions.
        /// </summary>
        Task<MemorySleep> SleepAsync(IReadOnlyList<MemoryCall> calls, IReadOnlyList<string> recalled, string runId, CancellationToken cancellation);
    }
}
