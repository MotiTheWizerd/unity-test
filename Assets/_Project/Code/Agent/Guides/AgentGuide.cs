using System;

namespace Forge.Agent.Guides
{
    /// <summary>
    /// One guide for the world-building agent: the know-how for a kind of work (the sizes and distances that look
    /// right, the order of steps, the pitfalls) that runs across tools, so no single tool's description holds it.
    /// </summary>
    public sealed class AgentGuide
    {
        public AgentGuide(string topic, string when, string text)
        {
            if (!AgentGuides.IsTopic(topic))
                throw new ArgumentException($"'{topic}' is not a guide topic: use lowercase snake_case, at most {AgentGuides.MaxTopicLength} characters.", nameof(topic));
            if (string.IsNullOrWhiteSpace(when))
                throw new ArgumentException("A guide needs to say when to read it.", nameof(when));
            if (when.Contains("\n"))
                throw new ArgumentException("When to read a guide is one line.", nameof(when));
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("A guide needs a text.", nameof(text));

            Topic = topic;
            When = when.Trim();
            Text = text.Trim();
        }

        /// <summary>The name the agent reads it by, e.g. "towns".</summary>
        public string Topic { get; }

        /// <summary>When to read it, e.g. "before laying out a town"; the index lists it beside the topic.</summary>
        public string When { get; }

        /// <summary>The guide itself, in Markdown.</summary>
        public string Text { get; }
    }
}
