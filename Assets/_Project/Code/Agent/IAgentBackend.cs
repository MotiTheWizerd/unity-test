using System;
using System.Threading;
using System.Threading.Tasks;

namespace Forge.Agent
{
    /// <summary>
    /// A model provider that works through requests with Forge's tools: Claude Code today, others later. A backend
    /// is made with the tool host its model calls; how the model reaches it (MCP, an API's tool calls) is the
    /// backend's business.
    /// </summary>
    public interface IAgentBackend : IDisposable
    {
        /// <summary>
        /// Works through <paramref name="turn"/> and completes when the model is done or has failed (reported as a
        /// <see cref="AgentEventKind.Finished"/> event), or is cancelled by <paramref name="cancellation"/>
        /// (an <see cref="OperationCanceledException"/>). <paramref name="report"/> may be called from any thread.
        /// </summary>
        Task RunAsync(AgentTurn turn, Action<AgentEvent> report, CancellationToken cancellation);
    }
}
