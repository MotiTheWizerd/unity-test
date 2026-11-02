using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.ClaudeCode
{
    /// <summary>
    /// Reads Claude Code's stream-json output (one JSON object per line) into agent events: the conversation id,
    /// what the agent says, problems worth telling the user, and how the request ended. Lines it doesn't need
    /// (thinking, tool echoes, rate limits) are skipped.
    /// </summary>
    public static class ClaudeStream
    {
        static readonly IReadOnlyList<AgentEvent> Nothing = Array.Empty<AgentEvent>();

        public static IReadOnlyList<AgentEvent> Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return Nothing;

            JObject message;
            try
            {
                message = JObject.Parse(line);
            }
            catch (JsonReaderException)
            {
                // Not an event (e.g. a stray warning line); the events around it still count.
                return Nothing;
            }

            switch ((string)message["type"])
            {
                case "system" when (string)message["subtype"] == "init":
                    return Started(message);
                case "assistant" when message["parent_tool_use_id"] == null || message["parent_tool_use_id"].Type == JTokenType.Null:
                    return Said(message);
                case "result":
                    return new[] { Finished(message) };
                default:
                    return Nothing;
            }
        }

        static IReadOnlyList<AgentEvent> Started(JObject message)
        {
            var events = new List<AgentEvent>();
            var sessionId = (string)message["session_id"];
            if (!string.IsNullOrEmpty(sessionId))
                events.Add(AgentEvent.ConversationStarted(sessionId));

            var forge = (message["mcp_servers"] as JArray)?.OfType<JObject>().FirstOrDefault(s => (string)s["name"] == ClaudeCodeArguments.ServerName);
            var status = (string)forge?["status"] ?? "missing";
            if (status != "connected")
                events.Add(AgentEvent.Notice($"Claude Code could not reach Forge's tools ({status}), so it can talk but not build."));

            var keySource = (string)message["apiKeySource"];
            if (!string.IsNullOrEmpty(keySource) && keySource != "none")
                events.Add(AgentEvent.Notice($"Claude Code is using an API key ({keySource}), so this is billed to that key, not a Claude subscription."));
            return events;
        }

        static IReadOnlyList<AgentEvent> Said(JObject message) =>
            (message["message"]?["content"] as JArray)?
            .OfType<JObject>()
            .Where(block => (string)block["type"] == "text")
            .Select(block => ((string)block["text"])?.Trim())
            .Where(text => !string.IsNullOrEmpty(text))
            .Select(AgentEvent.Said)
            .ToList()
            ?? Nothing;

        static AgentEvent Finished(JObject message)
        {
            var subtype = (string)message["subtype"];
            var failed = (bool?)message["is_error"] == true || (subtype != null && subtype != "success");
            if (!failed)
                return AgentEvent.Done();

            var reason = (string)message["result"];
            if (string.IsNullOrWhiteSpace(reason))
                reason = string.Join("; ", (message["errors"] as JArray)?.Select(e => e.ToString()) ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(reason))
                reason = subtype ?? "unknown error";
            return AgentEvent.Failure($"Claude Code stopped: {reason}");
        }
    }
}
