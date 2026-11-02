using System.Collections.Generic;
using System.Linq;
using System.Text;
using Forge.Agent;
using Forge.Agent.ClaudeCode;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Forge.Tests.Agent
{
    public class ClaudeCodeTests
    {
        static readonly ClaudeCodeSettings Settings = new ClaudeCodeSettings("claude", "sonnet", "", "C:/forge/agent");

        [TestCase("plain", "plain")]
        [TestCase("", "\"\"")]
        [TestCase("two words", "\"two words\"")]
        [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [TestCase("C:\\dir with space\\", "\"C:\\dir with space\\\\\"")]
        [TestCase("a\\\"b", "\"a\\\\\\\"b\"")]
        [TestCase("no\\quote\\needed", "no\\quote\\needed")]
        public void Quote_EscapesExactlyWhatTheParserNeeds(string argument, string expected)
        {
            Assert.That(CommandLine.Quote(argument), Is.EqualTo(expected));
        }

        [Test]
        public void Join_SplitsBackIntoTheSameArguments()
        {
            var arguments = new[]
            {
                "--print", "", "say \"hi\"", "C:\\a b\\", "two\nlines", "{\"k\":\"v w\"}", "a\\\\b", "trail\\", "tab\there",
            };

            Assert.That(Split(CommandLine.Join(arguments)), Is.EqualTo(arguments));
        }

        [Test]
        public void Arguments_GiveTheAgentForgesToolsAndNothingElse()
        {
            var arguments = ClaudeCodeArguments.For(Settings, "Build nicely.", "http://127.0.0.1:5000/mcp", "secret", null);

            Assert.That(ValueOf(arguments, "--setting-sources"), Is.Empty, "No settings files: the user's hooks and plugins stay out.");
            Assert.That(ValueOf(arguments, "--tools"), Is.Empty, "No built-in tools.");
            Assert.That(arguments, Does.Contain("--strict-mcp-config"));
            Assert.That(ValueOf(arguments, "--allowedTools"), Is.EqualTo("mcp__forge"));
            Assert.That(ValueOf(arguments, "--permission-mode"), Is.EqualTo("dontAsk"));
            Assert.That(ValueOf(arguments, "--output-format"), Is.EqualTo("stream-json"));
            Assert.That(ValueOf(arguments, "--system-prompt"), Is.EqualTo("Build nicely."));
            Assert.That(ValueOf(arguments, "--model"), Is.EqualTo("sonnet"));
            Assert.That(arguments, Has.No.Member("--effort"), "Empty settings are left to Claude Code.");
            Assert.That(arguments, Has.No.Member("--resume"));

            var server = JObject.Parse(ValueOf(arguments, "--mcp-config"))["mcpServers"]["forge"];
            Assert.That((string)server["type"], Is.EqualTo("http"));
            Assert.That((string)server["url"], Is.EqualTo("http://127.0.0.1:5000/mcp"));
            Assert.That((string)server["headers"]["Authorization"], Is.EqualTo("Bearer secret"));
        }

        [Test]
        public void Arguments_ResumeTheConversation_WhenThereIsOne()
        {
            var arguments = ClaudeCodeArguments.For(Settings, "Build.", "http://127.0.0.1:5000/mcp", "secret", "c1");

            Assert.That(ValueOf(arguments, "--resume"), Is.EqualTo("c1"));
        }

        [Test]
        public void Stream_Init_StartsTheConversation()
        {
            var events = ClaudeStream.Parse(
                "{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"s1\",\"mcp_servers\":[{\"name\":\"forge\",\"status\":\"connected\"}],\"apiKeySource\":\"none\"}");

            Assert.That(events.Single().Kind, Is.EqualTo(AgentEventKind.ConversationStarted));
            Assert.That(events.Single().Text, Is.EqualTo("s1"));
        }

        [Test]
        public void Stream_Init_WarnsWhenTheToolsAreUnreachable_OrAnApiKeyPays()
        {
            var events = ClaudeStream.Parse(
                "{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"s1\",\"mcp_servers\":[{\"name\":\"forge\",\"status\":\"failed\"}],\"apiKeySource\":\"ANTHROPIC_API_KEY\"}");

            var notices = events.Where(e => e.Kind == AgentEventKind.Notice).Select(e => e.Text).ToList();
            Assert.That(notices, Has.Some.Contains("could not reach Forge's tools"));
            Assert.That(notices, Has.Some.Contains("API key"));
        }

        [Test]
        public void Stream_AssistantText_IsSaid_ButThinkingAndToolUseAreNot()
        {
            var events = ClaudeStream.Parse(
                "{\"type\":\"assistant\",\"parent_tool_use_id\":null,\"message\":{\"content\":[" +
                "{\"type\":\"thinking\",\"thinking\":\"hmm\"},{\"type\":\"text\",\"text\":\" Building a fence. \"}," +
                "{\"type\":\"tool_use\",\"name\":\"mcp__forge__place_line\",\"input\":{}}]}}");

            Assert.That(events.Select(e => (e.Kind, e.Text)), Is.EqualTo(new[] { (AgentEventKind.Text, "Building a fence.") }));
        }

        [Test]
        public void Stream_SubagentText_IsSkipped()
        {
            var events = ClaudeStream.Parse(
                "{\"type\":\"assistant\",\"parent_tool_use_id\":\"t1\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"inner\"}]}}");

            Assert.That(events, Is.Empty);
        }

        [Test]
        public void Stream_Result_SaysHowTheRequestEnded()
        {
            var done = ClaudeStream.Parse("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"Built.\"}").Single();
            Assert.That((done.Kind, done.Failed), Is.EqualTo((AgentEventKind.Finished, false)));

            var failed = ClaudeStream.Parse("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":true,\"result\":\"API Error: 500\"}").Single();
            Assert.That(failed.Failed, Is.True);
            Assert.That(failed.Text, Does.Contain("API Error: 500"));

            var cut = ClaudeStream.Parse("{\"type\":\"result\",\"subtype\":\"error_max_turns\",\"is_error\":true}").Single();
            Assert.That(cut.Text, Does.Contain("error_max_turns"));
        }

        [Test]
        public void Stream_SkipsLinesThatAreNotEvents()
        {
            Assert.That(ClaudeStream.Parse("warning: something"), Is.Empty);
            Assert.That(ClaudeStream.Parse(""), Is.Empty);
            Assert.That(ClaudeStream.Parse("{\"type\":\"rate_limit_event\"}"), Is.Empty);
        }

        static string ValueOf(IReadOnlyList<string> arguments, string flag)
        {
            var index = arguments.ToList().IndexOf(flag);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"{flag} is missing.");
            return arguments[index + 1];
        }

        // The C runtime's rules for splitting a command line, as the started process reads it.
        static List<string> Split(string commandLine)
        {
            var arguments = new List<string>();
            var current = new StringBuilder();
            var quoted = false;
            var started = false;
            var i = 0;
            while (i < commandLine.Length)
            {
                var c = commandLine[i];
                if (c == '\\')
                {
                    var backslashes = 0;
                    while (i < commandLine.Length && commandLine[i] == '\\')
                    {
                        backslashes++;
                        i++;
                    }

                    if (i < commandLine.Length && commandLine[i] == '"')
                    {
                        current.Append('\\', backslashes / 2);
                        if (backslashes % 2 == 1)
                        {
                            current.Append('"');
                            i++;
                        }
                    }
                    else
                    {
                        current.Append('\\', backslashes);
                    }

                    started = true;
                    continue;
                }

                if (c == '"')
                {
                    quoted = !quoted;
                    started = true;
                }
                else if ((c == ' ' || c == '\t') && !quoted)
                {
                    if (started)
                        arguments.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
                else
                {
                    current.Append(c);
                    started = true;
                }

                i++;
            }

            if (started)
                arguments.Add(current.ToString());
            return arguments;
        }
    }
}
