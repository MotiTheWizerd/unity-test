using System;
using System.Linq;
using System.Threading.Tasks;
using Forge.Agent.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.Mcp
{
    /// <summary>
    /// Forge's tools over the Model Context Protocol (JSON-RPC 2.0): answers initialize, ping, tools/list and
    /// tools/call for one <see cref="IToolHost"/>, so any MCP client (Claude Code today) can use them. Transport-free;
    /// <see cref="McpHttpEndpoint"/> carries the messages.
    /// </summary>
    public sealed class McpServer
    {
        /// <summary>The newest protocol version spoken; offered when a client asks for one this server doesn't know.</summary>
        public const string LatestVersion = "2025-11-25";

        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;

        static readonly string[] SupportedVersions = { LatestVersion, "2025-06-18", "2025-03-26" };

        readonly IToolHost _tools;
        readonly string _name;
        readonly string _version;

        public McpServer(IToolHost tools, string name, string version)
        {
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _version = version ?? throw new ArgumentNullException(nameof(version));
        }

        /// <summary>Answers one raw message; null when it needs no answer (a notification).</summary>
        public async Task<string> HandleAsync(string json)
        {
            JToken token;
            try
            {
                token = JToken.Parse(json ?? string.Empty);
            }
            catch (JsonReaderException ex)
            {
                return Error(null, ParseError, $"Not JSON: {ex.Message}").ToString(Formatting.None);
            }

            if (!(token is JObject message))
                return Error(null, InvalidRequest, "Send one JSON-RPC message per request; batches are not supported.").ToString(Formatting.None);

            var answer = await HandleAsync(message).ConfigureAwait(false);
            return answer?.ToString(Formatting.None);
        }

        /// <summary>Answers one message; null for notifications and for responses, which get no answer.</summary>
        public async Task<JObject> HandleAsync(JObject message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var id = message["id"];
            var method = (string)message["method"];
            if (id == null || id.Type == JTokenType.Null)
                return null;
            if (method == null)
                return Error(id, InvalidRequest, "A request needs a method.");

            try
            {
                switch (method)
                {
                    case "initialize":
                        return Result(id, Initialize(message["params"] as JObject));
                    case "ping":
                        return Result(id, new JObject());
                    case "tools/list":
                        return Result(id, new JObject { ["tools"] = ListTools() });
                    case "tools/call":
                        return await CallToolAsync(id, message["params"] as JObject).ConfigureAwait(false);
                    default:
                        return Error(id, MethodNotFound, $"Method '{method}' is not supported.");
                }
            }
            catch (Exception ex)
            {
                return Error(id, InternalError, ex.Message);
            }
        }

        JObject Initialize(JObject parameters)
        {
            var requested = (string)parameters?["protocolVersion"];
            return new JObject
            {
                ["protocolVersion"] = SupportedVersions.Contains(requested) ? requested : LatestVersion,
                ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false } },
                ["serverInfo"] = new JObject { ["name"] = _name, ["version"] = _version },
            };
        }

        JArray ListTools() =>
            new JArray(_tools.Tools.Select(tool => new JObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema.DeepClone(),
            }));

        async Task<JObject> CallToolAsync(JToken id, JObject parameters)
        {
            var name = (string)parameters?["name"];
            if (name == null || _tools.Tools.All(tool => tool.Name != name))
                return Error(id, InvalidParams, $"Unknown tool '{name}'.");

            var arguments = parameters["arguments"] as JObject ?? new JObject();
            var result = await _tools.CallAsync(name, arguments).ConfigureAwait(false);
            var content = new JArray(new JObject { ["type"] = "text", ["text"] = result.Content });
            foreach (var picture in result.Pictures)
            {
                content.Add(new JObject
                {
                    ["type"] = "image",
                    ["data"] = Convert.ToBase64String(picture.Bytes),
                    ["mimeType"] = picture.MediaType,
                });
            }

            return Result(id, new JObject
            {
                ["content"] = content,
                ["isError"] = result.IsError,
            });
        }

        static JObject Result(JToken id, JObject result) =>
            new JObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = result };

        static JObject Error(JToken id, int code, string message) =>
            new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone() ?? JValue.CreateNull(),
                ["error"] = new JObject { ["code"] = code, ["message"] = message },
            };
    }
}
