using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Forge.Agent.Mcp;
using Forge.Agent.Tools;

namespace Forge.Agent.ClaudeCode
{
    /// <summary>
    /// Runs each request as one Claude Code process in print mode, under the user's own Claude login. Forge's
    /// tools reach it over a local MCP endpoint (started on the first request), and follow-up requests resume
    /// the same Claude Code conversation.
    /// </summary>
    public sealed class ClaudeCodeBackend : IAgentBackend
    {
        const string ServerVersion = "0.1.0";

        readonly ClaudeCodeSettings _settings;
        readonly IToolHost _tools;
        readonly object _gate = new object();
        McpHttpEndpoint _endpoint;
        bool _disposed;

        public ClaudeCodeBackend(ClaudeCodeSettings settings, IToolHost tools)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        }

        /// <summary>Raised, on a worker thread, when the tools' endpoint fails to serve a request.</summary>
        public event Action<Exception> EndpointFailed;

        public async Task RunAsync(AgentTurn turn, Action<AgentEvent> report, CancellationToken cancellation)
        {
            if (turn == null)
                throw new ArgumentNullException(nameof(turn));
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var endpoint = Endpoint();
            Directory.CreateDirectory(_settings.WorkingDirectory);
            var start = ClaudeProcess.StartInfo(_settings, ClaudeCodeArguments.For(_settings, turn.Instructions, endpoint.Url, endpoint.Token, turn.ConversationId));

            Process process;
            try
            {
                process = Process.Start(start);
            }
            catch (Win32Exception ex)
            {
                report(AgentEvent.Failure($"Couldn't start Claude Code ('{_settings.Executable}'): {ex.Message}. Is it installed, on PATH and logged in?"));
                return;
            }

            using (process)
            using (cancellation.Register(() => ClaudeProcess.Stop(process)))
            {
                var errors = process.StandardError.ReadToEndAsync();
                await ClaudeProcess.SendAsync(process, ClaudeInput.UserMessage(turn.Prompt, turn.Images)).ConfigureAwait(false);

                var finished = false;
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    foreach (var happened in ClaudeStream.Parse(line))
                    {
                        finished |= happened.Kind == AgentEventKind.Finished;
                        report(happened);
                    }
                }

                process.WaitForExit();
                var errorOutput = await errors.ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if (!finished)
                    report(AgentEvent.Failure($"Claude Code exited without finishing (exit code {process.ExitCode}). {ClaudeProcess.Tail(errorOutput)}".Trim()));
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _endpoint?.Dispose();
                _endpoint = null;
            }
        }

        McpHttpEndpoint Endpoint()
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ClaudeCodeBackend));
                if (_endpoint == null)
                {
                    _endpoint = new McpHttpEndpoint(new McpServer(_tools, ClaudeCodeArguments.ServerName, ServerVersion));
                    _endpoint.Failed += ex => EndpointFailed?.Invoke(ex);
                }

                return _endpoint;
            }
        }
    }
}
