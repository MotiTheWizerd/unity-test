using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        const int ErrorTailLength = 600;

        static readonly Encoding Utf8 = new UTF8Encoding(false);

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
            var start = new ProcessStartInfo
            {
                FileName = _settings.Executable,
                Arguments = CommandLine.Join(ClaudeCodeArguments.For(_settings, turn.Instructions, endpoint.Url, endpoint.Token, turn.ConversationId)),
                WorkingDirectory = _settings.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Utf8,
                StandardErrorEncoding = Utf8,
            };

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
            using (cancellation.Register(() => Stop(process)))
            {
                var errors = process.StandardError.ReadToEndAsync();
                await SendPromptAsync(process, turn.Prompt).ConfigureAwait(false);

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
                    report(AgentEvent.Failure($"Claude Code exited without finishing (exit code {process.ExitCode}). {Tail(errorOutput)}".Trim()));
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

        // The message goes in on stdin, as UTF-8, so the user's text never needs command-line quoting.
        static async Task SendPromptAsync(Process process, string prompt)
        {
            var bytes = Utf8.GetBytes(prompt);
            try
            {
                await process.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // It exited before reading (bad arguments, no login); its error output, read next, says why.
            }
        }

        static void Stop(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException)
            {
                // It exited between the check and the kill.
            }
            catch (Win32Exception)
            {
                // It is already exiting.
            }
        }

        static string Tail(string text)
        {
            text = text?.Trim() ?? string.Empty;
            return text.Length <= ErrorTailLength ? text : "…" + text.Substring(text.Length - ErrorTailLength);
        }
    }
}
