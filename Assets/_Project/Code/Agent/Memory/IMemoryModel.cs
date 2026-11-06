using System.Threading;
using System.Threading.Tasks;

namespace Forge.Agent.Memory
{
    /// <summary>
    /// The model that distills a sleep. The memory server writes the words and keeps what comes back; only the model
    /// call is ours, because it runs on the user's own Claude login, which the server can't reach.
    /// </summary>
    public interface IMemoryModel
    {
        /// <summary>One answer to <paramref name="message"/> under <paramref name="instruction"/> (its system prompt), with no tools.</summary>
        Task<string> AnswerAsync(string instruction, string message, CancellationToken cancellation);
    }
}
