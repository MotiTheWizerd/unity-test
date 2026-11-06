using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forge.Agent.ClaudeCode
{
    /// <summary>
    /// What a request writes to Claude Code's stdin in stream-json input: one user message per line, its pictures
    /// as base64 image blocks ahead of the words, the order in which the model reads a picture best.
    /// </summary>
    public static class ClaudeInput
    {
        public static string UserMessage(string text, IReadOnlyList<AgentImage> images)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var content = new JArray();
            foreach (var image in images ?? AgentImage.None)
            {
                content.Add(new JObject
                {
                    ["type"] = "image",
                    ["source"] = new JObject
                    {
                        ["type"] = "base64",
                        ["media_type"] = image.MediaType,
                        ["data"] = Convert.ToBase64String(image.Bytes),
                    },
                });
            }

            content.Add(new JObject { ["type"] = "text", ["text"] = text });
            return new JObject
            {
                ["type"] = "user",
                ["message"] = new JObject { ["role"] = "user", ["content"] = content },
            }.ToString(Formatting.None) + "\n";
        }
    }
}
