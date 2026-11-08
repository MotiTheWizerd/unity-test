using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Editing.Commands;

namespace Forge.Agent.Guides
{
    /// <summary>
    /// The agent's guides as a command: one guide's text by its topic, or the list of them. The agent's Claude Code
    /// has Forge's tools and no others, so it can't open the files; this is how a guide reaches it.
    /// </summary>
    public sealed class ReadGuideCommand : EditorCommand<ReadGuideCommand.Arguments, ReadGuideCommand.Result>
    {
        readonly AgentGuides _guides;

        public ReadGuideCommand(AgentGuides guides)
            : base("read_guide",
                "Reads a guide: the know-how for a kind of work, such as laying out a town or shaping mountains (the sizes " +
                "and distances that look right, the order of steps, the pitfalls). Your instructions list the guides and " +
                "when to read each; read one before you plan that kind of work. Leave out the topic to list them.")
        {
            _guides = guides ?? throw new ArgumentNullException(nameof(guides));
        }

        public sealed class Arguments
        {
            [Param("The guide's topic, e.g. \"towns\". Leave out to list the guides.", Optional = true)]
            public string Topic { get; set; }
        }

        public sealed class Result : ICommandResult
        {
            internal Result(string summary, AgentGuide guide, IReadOnlyList<Entry> guides)
            {
                Summary = summary;
                Topic = guide?.Topic;
                Text = guide?.Text;
                Guides = guides;
            }

            public string Summary { get; }

            /// <summary>The guide read; null when listing.</summary>
            public string Topic { get; }

            /// <summary>The guide itself, in Markdown; null when listing.</summary>
            public string Text { get; }

            /// <summary>Every guide and when to read it; null when one was read.</summary>
            public IReadOnlyList<Entry> Guides { get; }
        }

        public sealed class Entry
        {
            internal Entry(AgentGuide guide)
            {
                Topic = guide.Topic;
                When = guide.When;
            }

            public string Topic { get; }

            public string When { get; }
        }

        protected override Result Run(Arguments arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments.Topic))
            {
                var all = _guides.All;
                return new Result(all.Count == 1 ? "1 guide." : $"{all.Count} guides.", null, all.Select(guide => new Entry(guide)).ToList());
            }

            if (!_guides.TryGet(arguments.Topic, out var found))
                throw new CommandException(_guides.All.Count == 0
                    ? "There are no guides."
                    : $"There is no guide on \"{arguments.Topic.Trim()}\". The guides are: {string.Join(", ", _guides.All.Select(guide => guide.Topic))}.");
            return new Result($"Read the {found.Topic} guide.", found, null);
        }
    }
}
