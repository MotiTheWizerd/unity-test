using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Forge.Agent.Memory;

namespace Forge.Agent.ClaudeCode
{
    /// <summary>
    /// Distills the memory's sleeps with Claude Code, under the user's own Claude login and on the agent's model:
    /// one isolated answer per sleep (<see cref="ClaudeCodeArguments.ForDistill"/>), the message on stdin.
    /// </summary>
    public sealed class ClaudeCodeMemoryModel : IMemoryModel
    {
        // A distill reads the whole request; Semantix's own sleeps allow the same.
        static readonly TimeSpan Deadline = TimeSpan.FromMinutes(15);

        readonly ClaudeCodeSettings _settings;

        public ClaudeCodeMemoryModel(ClaudeCodeSettings settings) =>
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        public async Task<string> AnswerAsync(string instruction, string message, CancellationToken cancellation)
        {
            if (string.IsNullOrEmpty(instruction))
                throw new ArgumentException("The instruction is empty.", nameof(instruction));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("The message is empty.", nameof(message));

            Directory.CreateDirectory(_settings.WorkingDirectory);
            var instructionFile = Path.Combine(_settings.WorkingDirectory, $"distill-{Guid.NewGuid():N}.txt");
            File.WriteAllText(instructionFile, instruction, ClaudeProcess.Utf8);
            try
            {
                Process process;
                try
                {
                    process = Process.Start(ClaudeProcess.StartInfo(_settings, ClaudeCodeArguments.ForDistill(_settings, instructionFile)));
                }
                catch (Win32Exception ex)
                {
                    throw new InvalidOperationException($"couldn't start Claude Code ('{_settings.Executable}'): {ex.Message}");
                }

                using (process)
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                using (deadline.Token.Register(() => ClaudeProcess.Stop(process)))
                {
                    deadline.CancelAfter(Deadline);
                    var errors = process.StandardError.ReadToEndAsync();
                    var output = process.StandardOutput.ReadToEndAsync();
                    await ClaudeProcess.SendAsync(process, message).ConfigureAwait(false);
                    var answer = await output.ConfigureAwait(false);
                    var errorOutput = await errors.ConfigureAwait(false);
                    process.WaitForExit();

                    cancellation.ThrowIfCancellationRequested();
                    if (deadline.IsCancellationRequested)
                        throw new TimeoutException($"Claude Code took over {Deadline.TotalMinutes:0} minutes and was stopped");
                    if (process.ExitCode != 0)
                    {
                        var why = ClaudeProcess.Tail(string.IsNullOrWhiteSpace(errorOutput) ? answer : errorOutput);
                        throw new InvalidOperationException($"Claude Code exited with code {process.ExitCode}: {(why.Length > 0 ? why : "no output")}");
                    }

                    return answer.Trim();
                }
            }
            finally
            {
                try
                {
                    File.Delete(instructionFile);
                }
                catch (IOException)
                {
                    // Still held by an exiting process: a stray file in the agent's folder, nothing more.
                }
            }
        }
    }
}
