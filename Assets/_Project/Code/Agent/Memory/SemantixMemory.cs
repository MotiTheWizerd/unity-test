using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Memory
{
    /// <summary>
    /// The agent's memory in Semantix: one memory agent on the user's Semantix account, found by name (and made on
    /// first use), read and written through the same doors as Semantix's own Claude Code tasks:
    /// <c>/dress</c> before a turn, and a sleep in two halves around the model, <c>/sleep/prompt</c> (the server
    /// writes the distill's words), the user's own model, then <c>/sleep/reply</c> (the server keeps the memories).
    /// The same agent is used both ways, so what is kept is what is recalled.
    /// </summary>
    public sealed class SemantixMemory : IAgentMemory
    {
        const string Agents = "/api/v1/memory/agents";

        /// <summary>Recall reads no more of the cue than this (the server's own cap).</summary>
        internal const int CueLength = 8000;

        // SHORTCUT: the agent is made with origin "user", which Semantix Studio doesn't list, because the server knows
        // no "forge" origin yet. It comes with letting the user make memories at will.
        const string Origin = "user";

        static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(20);
        // Embedding, recall and standing orders: seconds, not minutes.
        static readonly TimeSpan DressTimeout = TimeSpan.FromSeconds(60);
        // The server's half of a sleep: reading the index, embedding what it writes.
        static readonly TimeSpan SleepTimeout = TimeSpan.FromMinutes(5);

        readonly ISemantixHttp _http;
        readonly IMemoryModel _model;
        readonly string _agentName;
        readonly SemaphoreSlim _resolving = new SemaphoreSlim(1, 1);
        string _agentId;

        /// <param name="agentName">The memory agent's name on the user's account, e.g. "Forge".</param>
        public SemantixMemory(ISemantixHttp http, IMemoryModel model, string agentName)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(agentName))
                throw new ArgumentException("The memory's name is required.", nameof(agentName));
            _agentName = agentName.Trim();
        }

        public async Task<MemoryDress> DressAsync(string cue, CancellationToken cancellation)
        {
            cue = cue?.Trim();
            if (string.IsNullOrEmpty(cue))
                throw new ArgumentException("The cue is empty.", nameof(cue));
            if (cue.Length > CueLength)
                cue = cue.Substring(0, char.IsHighSurrogate(cue[CueLength - 1]) ? CueLength - 1 : CueLength);

            var agent = await AgentIdAsync(cancellation).ConfigureAwait(false);
            JToken reply;
            try
            {
                reply = await _http.PostAsync($"{Agents}/{agent}/dress", new JObject { ["query"] = cue }, DressTimeout, cancellation).ConfigureAwait(false);
            }
            catch (SemantixException ex) when (ex.Status == 404)
            {
                // Deleted in Semantix since it was found: the next turn looks for it (or makes it) again.
                Forget(agent);
                throw;
            }

            var recalled = (reply["recalled"] as JArray)?.Select(name => (string)name).Where(name => !string.IsNullOrEmpty(name)).ToList();
            return new MemoryDress((string)reply["block"], recalled);
        }

        public async Task<MemorySleep> SleepAsync(IReadOnlyList<MemoryCall> calls, IReadOnlyList<string> recalled, string runId, CancellationToken cancellation)
        {
            if (calls == null || calls.Count == 0)
                return MemorySleep.Skip("nothing was said");

            try
            {
                var agent = await AgentIdAsync(cancellation).ConfigureAwait(false);
                var run = SleepBody(calls, recalled, runId);
                var words = await _http.PostAsync($"{Agents}/{agent}/sleep/prompt", run, SleepTimeout, cancellation).ConfigureAwait(false);
                var instruction = (string)words["instruction"];
                var message = (string)words["message"];
                // No words: the server skipped or refused before a model was needed, and its line says why.
                if (string.IsNullOrEmpty(instruction) || string.IsNullOrEmpty(message))
                    return Line(words);

                var answer = await _model.AnswerAsync(instruction, message, cancellation).ConfigureAwait(false);
                run["reply"] = answer ?? string.Empty;
                return Line(await _http.PostAsync($"{Agents}/{agent}/sleep/reply", run, SleepTimeout, cancellation).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return MemorySleep.Fail(ex.Message);
            }
        }

        /// <summary>The request as both sleep doors take it: the same body twice, so the server rebuilds the pass rather than trusting a copy.</summary>
        internal static JObject SleepBody(IReadOnlyList<MemoryCall> calls, IReadOnlyList<string> recalled, string runId)
        {
            var body = new JObject
            {
                ["calls"] = new JArray(calls.Select(call => new JObject
                {
                    ["prompt"] = call.Prompt,
                    ["answer"] = call.Answer,
                    ["tool_trail"] = new JArray(call.ToolTrail),
                })),
                ["recalled"] = new JArray((recalled ?? Array.Empty<string>()).Distinct()),
            };
            if (!string.IsNullOrEmpty(runId))
                body["run_id"] = runId;
            return body;
        }

        static MemorySleep Line(JToken line) => new MemorySleep(
            (int?)line["created"] ?? 0,
            (int?)line["updated"] ?? 0,
            (line["names"] as JArray)?.Select(name => (string)name).ToList(),
            (string)line["skipped"],
            (string)line["error"]);

        async Task<string> AgentIdAsync(CancellationToken cancellation)
        {
            var known = Volatile.Read(ref _agentId);
            if (known != null)
                return known;

            await _resolving.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                if (_agentId != null)
                    return _agentId;

                var agents = await _http.GetAsync(Agents, LookupTimeout, cancellation).ConfigureAwait(false);
                var found = (agents as JArray)?.OfType<JObject>().FirstOrDefault(agent => (string)agent["name"] == _agentName);
                if (found == null)
                {
                    found = await _http.PostAsync(Agents, new JObject
                    {
                        ["name"] = _agentName,
                        ["description"] = "Forge's world-building agent: what it learns building worlds with you.",
                        ["origin"] = Origin,
                        ["memory_enabled"] = true,
                    }, LookupTimeout, cancellation).ConfigureAwait(false) as JObject;
                }

                var id = (string)found?["agent_id"];
                if (string.IsNullOrEmpty(id))
                    throw new SemantixException($"Semantix gave no id for the memory '{_agentName}'");
                Volatile.Write(ref _agentId, id);
                return id;
            }
            finally
            {
                _resolving.Release();
            }
        }

        void Forget(string agent) => Interlocked.CompareExchange(ref _agentId, null, agent);
    }
}
