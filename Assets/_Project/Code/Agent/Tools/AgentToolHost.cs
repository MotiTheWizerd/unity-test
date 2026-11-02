using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Tools
{
    /// <summary>
    /// The tools as the agent reaches them: each call, from whatever thread the backend uses, is carried to the
    /// main thread and run there, because the edit session isn't thread-safe. Calls are taken only while a
    /// request is being worked on (<see cref="Open"/>), and each one is reported for the transcript.
    /// </summary>
    public sealed class AgentToolHost : IToolHost
    {
        readonly ToolInvoker _invoker;
        readonly MainThreadQueue _mainThread;
        bool _open;

        public AgentToolHost(ToolInvoker invoker, MainThreadQueue mainThread)
        {
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
        }

        /// <summary>Raised on the main thread after every call.</summary>
        public event Action<ToolCall> Called;

        public IReadOnlyList<ToolDefinition> Tools => _invoker.Tools;

        /// <summary>Starts taking calls (main thread).</summary>
        public void Open() => _open = true;

        /// <summary>Refuses calls from now on (main thread), e.g. a stray one after its request was stopped.</summary>
        public void Close() => _open = false;

        public Task<ToolResult> CallAsync(string name, JObject arguments) =>
            _mainThread.Run(() =>
            {
                var result = _open
                    ? _invoker.Invoke(name, arguments)
                    : ToolResult.Failure("No request is being worked on, so the world can't be changed now.");
                Called?.Invoke(new ToolCall(name, result));
                return result;
            });
    }
}
