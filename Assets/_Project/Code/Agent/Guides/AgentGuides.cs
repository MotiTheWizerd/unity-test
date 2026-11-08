using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Forge.Agent.Guides
{
    /// <summary>
    /// The world-building agent's guides, by topic. Each is a Markdown file in one folder (the editor's is
    /// StreamingAssets/AgentGuides), named after its topic (towns.md), that opens with a front matter saying when to
    /// read it:
    /// <code>
    /// ---
    /// when: before laying out a town or village
    /// ---
    /// # Towns
    /// …
    /// </code>
    /// The agent's tools can't read files, so a guide reaches it through read_guide; its instructions end with the
    /// index (<see cref="AppendIndex"/>), so it knows which guides there are and when each applies.
    /// </summary>
    public sealed class AgentGuides
    {
        public const string Extension = ".md";

        /// <summary>Longest topic name.</summary>
        public const int MaxTopicLength = 32;

        const string Fence = "---";
        const string WhenField = "when";

        // How the index tells the agent to use the guides; the list of them follows.
        const string IndexIntroduction =
            "Guides hold the know-how for kinds of work that no single tool's description has: the sizes and distances " +
            "that look right, the order of steps, and the pitfalls. Before you plan work of a kind listed here, read its " +
            "guide with `read_guide`; for work of several kinds, read each. Once read, a guide holds for the rest of the " +
            "conversation. Where a guide and a tool's own description disagree, the tool is right.";

        public static readonly AgentGuides None = new AgentGuides(Array.Empty<AgentGuide>());

        readonly Dictionary<string, AgentGuide> _byTopic = new Dictionary<string, AgentGuide>(StringComparer.OrdinalIgnoreCase);

        public AgentGuides(IEnumerable<AgentGuide> guides)
        {
            if (guides == null)
                throw new ArgumentNullException(nameof(guides));

            foreach (var guide in guides)
            {
                if (guide == null)
                    throw new ArgumentException("A guide is null.", nameof(guides));
                if (_byTopic.ContainsKey(guide.Topic))
                    throw new ArgumentException($"Two guides on '{guide.Topic}'.", nameof(guides));
                _byTopic.Add(guide.Topic, guide);
            }

            All = _byTopic.Values.OrderBy(guide => guide.Topic, StringComparer.Ordinal).ToList();
        }

        /// <summary>The guides, sorted by topic.</summary>
        public IReadOnlyList<AgentGuide> All { get; }

        /// <summary>The guide on <paramref name="topic"/>, in any case.</summary>
        public bool TryGet(string topic, out AgentGuide guide)
        {
            guide = null;
            return topic != null && _byTopic.TryGetValue(topic.Trim(), out guide);
        }

        /// <summary>
        /// The agent's instructions with the index after them: a section listing each guide's topic and when to read
        /// it. Without guides the instructions stay as they are.
        /// </summary>
        public string AppendIndex(string instructions)
        {
            if (instructions == null)
                throw new ArgumentNullException(nameof(instructions));
            if (All.Count == 0)
                return instructions;

            var text = new StringBuilder(instructions.TrimEnd());
            text.Append("\n\n## Guides\n\n").Append(IndexIntroduction).Append("\n\n");
            foreach (var guide in All)
                text.Append("- `").Append(guide.Topic).Append("`: ").Append(guide.When).Append('\n');
            return text.ToString();
        }

        /// <summary>Whether <paramref name="name"/> can be a topic: lowercase snake_case, as command names are.</summary>
        public static bool IsTopic(string name) =>
            !string.IsNullOrEmpty(name) && name.Length <= MaxTopicLength
            && name[0] >= 'a' && name[0] <= 'z'
            && name.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');

        /// <summary>Every guide in <paramref name="folder"/>: each Markdown file, its topic its name.</summary>
        /// <exception cref="DirectoryNotFoundException">The folder is missing.</exception>
        /// <exception cref="InvalidDataException">A file is misnamed or malformed; the message names it.</exception>
        public static AgentGuides Read(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                throw new ArgumentException("The guides' folder is required.", nameof(folder));
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"The agent's guides folder is missing: {folder}");

            var guides = new List<AgentGuide>();
            var files = Directory.GetFiles(folder)
                .Where(path => string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal);
            foreach (var path in files)
            {
                var topic = Path.GetFileNameWithoutExtension(path);
                if (!IsTopic(topic))
                    throw new InvalidDataException($"The guide file '{Path.GetFileName(path)}' is not named after a topic: use lowercase snake_case, e.g. towns{Extension} ({path}).");

                try
                {
                    guides.Add(Parse(topic, File.ReadAllText(path)));
                }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException($"{ex.Message} ({path})", ex);
                }
            }

            return new AgentGuides(guides);
        }

        /// <summary>A guide from a file's text: the front matter (when to read it), then the guide.</summary>
        /// <exception cref="InvalidDataException">The text is malformed; the message says how.</exception>
        public static AgentGuide Parse(string topic, string content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            var lines = content.Replace("\r\n", "\n").Split('\n');
            if (lines[0].Trim() != Fence)
                throw new InvalidDataException($"The guide '{topic}' must open with a front matter: a line \"{Fence}\", then \"{WhenField}: …\", then \"{Fence}\" again.");

            string when = null;
            var end = -1;
            for (var i = 1; i < lines.Length && end < 0; i++)
            {
                var line = lines[i].Trim();
                if (line == Fence)
                {
                    end = i;
                    continue;
                }

                if (line.Length == 0)
                    continue;
                var colon = line.IndexOf(':');
                if (colon <= 0)
                    throw new InvalidDataException($"The guide '{topic}' has a front matter line that is not \"field: value\": \"{line}\" (is the closing \"{Fence}\" missing?).");
                var field = line.Substring(0, colon).Trim();
                if (field != WhenField)
                    throw new InvalidDataException($"The guide '{topic}' has an unknown front matter field '{field}'; the only one is '{WhenField}'.");
                if (when != null)
                    throw new InvalidDataException($"The guide '{topic}' says '{WhenField}' twice.");
                when = line.Substring(colon + 1).Trim();
            }

            if (end < 0)
                throw new InvalidDataException($"The guide '{topic}' has no closing \"{Fence}\" after its front matter.");
            if (string.IsNullOrEmpty(when))
                throw new InvalidDataException($"The guide '{topic}' must say '{WhenField}' to read it in its front matter.");

            var text = string.Join("\n", lines.Skip(end + 1)).Trim();
            if (text.Length == 0)
                throw new InvalidDataException($"The guide '{topic}' has no text after its front matter.");
            return new AgentGuide(topic, when, text);
        }
    }
}
