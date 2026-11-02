using System;

namespace Forge.Agent.ClaudeCode
{
    /// <summary>How to run Claude Code for the agent.</summary>
    public sealed class ClaudeCodeSettings
    {
        /// <param name="executable">The Claude Code command ("claude", found on PATH) or a full path to it.</param>
        /// <param name="model">Model alias or name ("sonnet", "opus"); empty for Claude Code's default.</param>
        /// <param name="effort">Effort level (low, medium, high, xhigh, max); empty for the default.</param>
        /// <param name="workingDirectory">Where it runs and keeps its conversations, apart from any project's files.</param>
        public ClaudeCodeSettings(string executable, string model, string effort, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(executable))
                throw new ArgumentException("The Claude Code command is required.", nameof(executable));
            if (string.IsNullOrWhiteSpace(workingDirectory))
                throw new ArgumentException("A working directory is required.", nameof(workingDirectory));

            Executable = executable.Trim();
            Model = model?.Trim() ?? string.Empty;
            Effort = effort?.Trim() ?? string.Empty;
            WorkingDirectory = workingDirectory;
        }

        public string Executable { get; }

        public string Model { get; }

        public string Effort { get; }

        public string WorkingDirectory { get; }
    }
}
