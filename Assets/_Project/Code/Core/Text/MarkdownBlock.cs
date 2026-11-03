using System;
using System.Collections.Generic;

namespace Forge.Core.Text
{
    public enum MarkdownBlockKind
    {
        Paragraph,
        Heading,
        Bullet,
        Numbered,
        Code,
    }

    /// <summary>One block of a Markdown text: a paragraph, heading, list item or code block.</summary>
    public sealed class MarkdownBlock
    {
        public MarkdownBlock(MarkdownBlockKind kind, int level, string marker, IReadOnlyList<MarkdownSpan> spans)
        {
            Kind = kind;
            Level = level;
            Marker = marker;
            Spans = spans ?? throw new ArgumentNullException(nameof(spans));
        }

        public MarkdownBlockKind Kind { get; }

        /// <summary>A list item's nesting depth (0 at the margin), or a heading's rank (0 for #).</summary>
        public int Level { get; }

        /// <summary>A numbered item's number as written, e.g. "3."; null for other blocks.</summary>
        public string Marker { get; }

        public IReadOnlyList<MarkdownSpan> Spans { get; }
    }
}
