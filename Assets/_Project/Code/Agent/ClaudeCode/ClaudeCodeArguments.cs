using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.ClaudeCode
{
    /// <summary>
    /// The command line of one request in Claude Code's print mode. The agent gets Forge's tools and nothing else:
    /// no built-in tools (files, shell, web), no other MCP servers, and none of the user's settings files, so their
    /// hooks, plugins and permission rules stay out. The login (a Claude subscription) is used as it is.
    /// </summary>
    public static class ClaudeCodeArguments
    {
        /// <summary>The MCP server name Forge's tools appear under ("mcp__forge__place_line").</summary>
        public const string ServerName = "forge";

        public static IReadOnlyList<string> For(ClaudeCodeSettings settings, string instructions, string toolsUrl, string toolsToken, string resumeConversationId)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var arguments = new List<string>
            {
                "--print",
                // The message goes in as JSON (ClaudeInput), so it can carry pictures.
                "--input-format", "stream-json",
                // Events as they happen, one JSON object per line; stream-json needs --verbose.
                "--output-format", "stream-json",
                "--verbose",
                "--setting-sources", string.Empty,
                "--tools", string.Empty,
                "--strict-mcp-config",
                "--mcp-config", McpConfig(toolsUrl, toolsToken),
                "--allowedTools", "mcp__" + ServerName,
                // Anything not allowed above is refused, never asked: nobody is there to answer.
                "--permission-mode", "dontAsk",
                "--system-prompt", instructions ?? throw new ArgumentNullException(nameof(instructions)),
            };

            if (settings.Model.Length > 0)
                arguments.AddRange(new[] { "--model", settings.Model });
            if (settings.Effort.Length > 0)
                arguments.AddRange(new[] { "--effort", settings.Effort });
            if (!string.IsNullOrEmpty(resumeConversationId))
                arguments.AddRange(new[] { "--resume", resumeConversationId });
            return arguments;
        }

        /// <summary>
        /// The command line of one memory distill: a single answer in plain text, with no tools, no MCP servers, no
        /// settings files and no conversation left behind. The instruction is read from a file because it is long.
        /// </summary>
        public static IReadOnlyList<string> ForDistill(ClaudeCodeSettings settings, string instructionFile)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrEmpty(instructionFile))
                throw new ArgumentException("The instruction file is required.", nameof(instructionFile));

            var arguments = new List<string>
            {
                "--print",
                "--setting-sources", string.Empty,
                "--tools", string.Empty,
                "--strict-mcp-config",
                "--no-session-persistence",
                "--system-prompt-file", instructionFile,
            };
            if (settings.Model.Length > 0)
                arguments.AddRange(new[] { "--model", settings.Model });
            return arguments;
        }

        /// <summary>The --mcp-config JSON pointing Claude Code at Forge's endpoint.</summary>
        public static string McpConfig(string url, string token)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("The tools' URL is required.", nameof(url));
            if (string.IsNullOrEmpty(token))
                throw new ArgumentException("The tools' token is required.", nameof(token));

            return new JObject
            {
                ["mcpServers"] = new JObject
                {
                    [ServerName] = new JObject
                    {
                        ["type"] = "http",
                        ["url"] = url,
                        ["headers"] = new JObject { ["Authorization"] = "Bearer " + token },
                    },
                },
            }.ToString(Formatting.None);
        }
    }
}
