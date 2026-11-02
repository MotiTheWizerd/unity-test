using System;
using Forge.Agent.ClaudeCode;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// How the world-building agent runs: the Claude Code command and model it uses, and its standing
    /// instructions (a text asset, so they can be edited without touching code).
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Agent Settings", fileName = "AgentSettings")]
    public sealed class AgentSettings : ScriptableObject
    {
        [SerializeField, Tooltip("The Claude Code command (found on PATH) or a full path to it.")]
        string _executable = "claude";

        [SerializeField, Tooltip("Model alias or name, e.g. sonnet or opus; empty for Claude Code's default.")]
        string _model = "sonnet";

        [SerializeField, Tooltip("Effort: low, medium, high, xhigh or max; empty for Claude Code's default.")]
        string _effort = "";

        [SerializeField, Tooltip("The agent's standing instructions (its system prompt).")]
        TextAsset _instructions;

        /// <summary>The agent's standing instructions; fails loudly if the asset has none.</summary>
        public string Instructions =>
            _instructions != null && !string.IsNullOrWhiteSpace(_instructions.text)
                ? _instructions.text
                : throw new InvalidOperationException($"Agent settings '{name}' have no instructions assigned.");

        /// <param name="workingDirectory">Where Claude Code runs and keeps the agent's conversations.</param>
        public ClaudeCodeSettings ClaudeCode(string workingDirectory) =>
            new ClaudeCodeSettings(_executable, _model, _effort, workingDirectory);
    }
}
