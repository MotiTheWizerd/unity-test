using System;
using System.IO;
using Forge.Agent.ClaudeCode;
using Forge.Agent.Guides;
using UnityEngine;

namespace Forge.EditorApp.Agent
{
    /// <summary>
    /// How the world-building agent runs: the Claude Code command and model it uses, its standing
    /// instructions (a text asset, so they can be edited without touching code), the folder of its guides,
    /// and its memory in Semantix.
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

        [SerializeField, Tooltip("Folder under StreamingAssets holding the agent's guides, one Markdown file per topic; empty for none.")]
        string _guidesFolder = "AgentGuides";

        [Header("Memory")]
        [SerializeField, Tooltip("Keep the agent's long-term memory in Semantix. Needs the Semantix app signed in on this machine.")]
        bool _keepsMemory = true;

        [SerializeField, Tooltip("The Semantix server. The SEMANTIX_API_URL environment variable overrides it.")]
        string _memoryServer = "https://api.semantixstudio.com";

        [SerializeField, Tooltip("The memory's name on the Semantix account; it is made on first use.")]
        string _memoryName = "Forge";

        public bool KeepsMemory => _keepsMemory;

        public string MemoryServer => _memoryServer;

        public string MemoryName => _memoryName;

        /// <summary>The agent's standing instructions; fails loudly if the asset has none.</summary>
        public string Instructions =>
            _instructions != null && !string.IsNullOrWhiteSpace(_instructions.text)
                ? _instructions.text
                : throw new InvalidOperationException($"Agent settings '{name}' have no instructions assigned.");

        /// <summary>The agent's guides, read from their folder under <paramref name="streamingAssets"/>; none when no folder is set.</summary>
        public AgentGuides Guides(string streamingAssets) =>
            string.IsNullOrEmpty(_guidesFolder) ? AgentGuides.None : AgentGuides.Read(Path.Combine(streamingAssets, _guidesFolder));

        /// <param name="workingDirectory">Where Claude Code runs and keeps the agent's conversations.</param>
        public ClaudeCodeSettings ClaudeCode(string workingDirectory) =>
            new ClaudeCodeSettings(_executable, _model, _effort, workingDirectory);
    }
}
