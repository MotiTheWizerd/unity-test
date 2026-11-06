using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Forge.Agent.ClaudeCode
{
    /// <summary>Starting, feeding and stopping one Claude Code process, shared by the agent's requests and the memory's distills.</summary>
    static class ClaudeProcess
    {
        const int ErrorTailLength = 600;

        // Claude Code's own auto memory stays off: the agent's memory is the one Forge dresses it with.
        const string NoAutoMemory = "CLAUDE_CODE_DISABLE_AUTO_MEMORY";

        public static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static ProcessStartInfo StartInfo(ClaudeCodeSettings settings, IEnumerable<string> arguments)
        {
            var start = new ProcessStartInfo
            {
                FileName = settings.Executable,
                Arguments = CommandLine.Join(arguments),
                WorkingDirectory = settings.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Utf8,
                StandardErrorEncoding = Utf8,
            };
            start.EnvironmentVariables[NoAutoMemory] = "1";
            return start;
        }

        // Text goes in on stdin, as UTF-8, so it never needs command-line quoting (or fits a command line's length).
        public static async Task SendAsync(Process process, string text)
        {
            var bytes = Utf8.GetBytes(text);
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

        public static void Stop(Process process)
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

        public static string Tail(string text)
        {
            text = text?.Trim() ?? string.Empty;
            return text.Length <= ErrorTailLength ? text : "…" + text.Substring(text.Length - ErrorTailLength);
        }
    }
}
