using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Forge.Agent.Mcp;
using Forge.Agent.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Forge.Tests.Agent
{
    public class McpServerTests
    {
        const string Initialize = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\"}}";
        const string Initialized = "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}";

        sealed class EchoTools : IToolHost
        {
            public IReadOnlyList<ToolDefinition> Tools { get; } = new[]
            {
                new ToolDefinition("echo", "Says the arguments back.", JObject.Parse("{\"type\":\"object\"}")),
            };

            public Task<ToolResult> CallAsync(string name, JObject arguments) =>
                Task.FromResult(ToolResult.Success(arguments.ToString(Formatting.None), "echoed", Pictures));

            public IReadOnlyList<Forge.Agent.AgentImage> Pictures { get; set; }
        }

        [Test]
        public void ToolsCall_APictureFollowsTheTextAsAnImageBlock()
        {
            var tools = new EchoTools { Pictures = new[] { new Forge.Agent.AgentImage("image/jpeg", new byte[] { 1, 2, 3 }) } };
            var answer = JObject.Parse(new McpServer(tools, "forge", "0.1.0")
                .HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"echo\",\"arguments\":{}}}")
                .GetAwaiter().GetResult());

            var content = (JArray)answer["result"]["content"];
            Assert.That(content.Select(c => (string)c["type"]), Is.EqualTo(new[] { "text", "image" }));
            Assert.That((string)content[1]["data"], Is.EqualTo("AQID"));
            Assert.That((string)content[1]["mimeType"], Is.EqualTo("image/jpeg"));
        }

        static McpServer NewServer() => new McpServer(new EchoTools(), "forge", "0.1.0");

        static JObject Ask(string json)
        {
            var answer = NewServer().HandleAsync(json).GetAwaiter().GetResult();
            return answer == null ? null : JObject.Parse(answer);
        }

        [TestCase("2025-06-18", "2025-06-18")]
        [TestCase("2099-01-01", McpServer.LatestVersion)]
        public void Initialize_AgreesOnAProtocolVersion(string asked, string agreed)
        {
            var answer = Ask("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" + asked + "\"}}");

            Assert.That((string)answer["result"]["protocolVersion"], Is.EqualTo(agreed));
            Assert.That(answer["result"]["capabilities"]["tools"], Is.Not.Null);
            Assert.That((string)answer["result"]["serverInfo"]["name"], Is.EqualTo("forge"));
            Assert.That((int)answer["id"], Is.EqualTo(1));
        }

        [Test]
        public void ToolsList_DescribesEveryTool()
        {
            var answer = Ask("{\"jsonrpc\":\"2.0\",\"id\":\"a\",\"method\":\"tools/list\"}");

            var tool = answer["result"]["tools"].Single();
            Assert.That((string)tool["name"], Is.EqualTo("echo"));
            Assert.That((string)tool["inputSchema"]["type"], Is.EqualTo("object"));
            Assert.That((string)answer["id"], Is.EqualTo("a"));
        }

        [Test]
        public void ToolsCall_AnswersWithTheResultAsText()
        {
            var answer = Ask("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"echo\",\"arguments\":{\"say\":\"hi\"}}}");

            var content = answer["result"]["content"].Single();
            Assert.That((string)content["type"], Is.EqualTo("text"));
            Assert.That((string)content["text"], Is.EqualTo("{\"say\":\"hi\"}"));
            Assert.That((bool)answer["result"]["isError"], Is.False);
        }

        [Test]
        public void ToolsCall_AnUnknownTool_IsAnInvalidParamsError()
        {
            var answer = Ask("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"nope\"}}");

            Assert.That((int)answer["error"]["code"], Is.EqualTo(McpServer.InvalidParams));
        }

        [Test]
        public void Notifications_GetNoAnswer()
        {
            Assert.That(Ask(Initialized), Is.Null);
        }

        [Test]
        public void UnknownMethods_AreNotFound_SoClientsFallBack()
        {
            var answer = Ask("{\"jsonrpc\":\"2.0\",\"id\":\"probe\",\"method\":\"server/discover\"}");

            Assert.That((int)answer["error"]["code"], Is.EqualTo(McpServer.MethodNotFound));
        }

        [Test]
        public void BrokenMessages_AreJsonRpcErrors()
        {
            Assert.That((int)Ask("{nope")["error"]["code"], Is.EqualTo(McpServer.ParseError));
            Assert.That((int)Ask("[" + Initialize + "]")["error"]["code"], Is.EqualTo(McpServer.InvalidRequest));
        }

        [Test]
        public void Endpoint_ServesOnlyAuthorizedPostsOnTheLoopback()
        {
            using (var endpoint = new McpHttpEndpoint(NewServer()))
            using (var http = new HttpClient())
            {
                Assert.That(endpoint.Url, Does.StartWith("http://127.0.0.1:"));

                Assert.That(Send(http, HttpMethod.Post, endpoint.Url, null, Initialize).Status, Is.EqualTo(HttpStatusCode.Unauthorized));

                var initialized = Send(http, HttpMethod.Post, endpoint.Url, endpoint.Token, Initialize);
                Assert.That(initialized.Status, Is.EqualTo(HttpStatusCode.OK));
                Assert.That((string)JObject.Parse(initialized.Body)["result"]["protocolVersion"], Is.EqualTo("2025-06-18"));

                Assert.That(Send(http, HttpMethod.Post, endpoint.Url, endpoint.Token, Initialized).Status, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(Send(http, HttpMethod.Get, endpoint.Url, endpoint.Token, null).Status, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
                Assert.That(Send(http, HttpMethod.Post, endpoint.Url, endpoint.Token, Initialize, origin: "https://example.com").Status,
                    Is.EqualTo(HttpStatusCode.Forbidden));
            }
        }

        // On a worker thread: blocking the test's thread on a task that captured it could deadlock.
        static (HttpStatusCode Status, string Body) Send(HttpClient http, HttpMethod method, string url, string token, string body, string origin = null) =>
            Task.Run(async () =>
            {
                var request = new HttpRequestMessage(method, url);
                if (token != null)
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                if (origin != null)
                    request.Headers.TryAddWithoutValidation("Origin", origin);
                if (body != null)
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using (var response = await http.SendAsync(request))
                    return (response.StatusCode, await response.Content.ReadAsStringAsync());
            }).GetAwaiter().GetResult();
    }
}
