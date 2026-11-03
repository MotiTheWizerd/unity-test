using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Forge.Core.Text
{
    /// <summary>
    /// Reads the small part of Markdown that chat replies use: paragraphs, headings, bullet and numbered lists
    /// (nested by indent), fenced code, and bold, italic and inline code inside text. Anything else stays as
    /// written. Lines of one paragraph are joined with spaces, as Markdown does; a blank line ends a block.
    /// </summary>
    public static class Markdown
    {
        // Two spaces of indent (or a tab) per list level.
        const int IndentPerLevel = 2;

        static readonly Regex Heading = new Regex(@"^(#{1,6})\s+(.*?)\s*#*\s*$");
        static readonly Regex Bullet = new Regex(@"^([ \t]*)[-*+]\s+(.*)$");
        static readonly Regex Numbered = new Regex(@"^([ \t]*)(\d{1,9})[.)]\s+(.*)$");
        static readonly Regex Fence = new Regex(@"^\s*(```|~~~)");

        public static IReadOnlyList<MarkdownBlock> Parse(string text)
        {
            var blocks = new List<MarkdownBlock>();
            if (string.IsNullOrEmpty(text))
                return blocks;

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var open = (Block)null;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (Fence.IsMatch(line))
                {
                    Close(ref open, blocks);
                    i = ReadCode(lines, i, blocks);
                    continue;
                }

                if (line.Trim().Length == 0 || IsRule(line))
                {
                    Close(ref open, blocks);
                    continue;
                }

                Match match;
                if ((match = Heading.Match(line)).Success)
                {
                    Close(ref open, blocks);
                    blocks.Add(new MarkdownBlock(MarkdownBlockKind.Heading, match.Groups[1].Length - 1, null, Inline(match.Groups[2].Value)));
                }
                else if ((match = Bullet.Match(line)).Success)
                {
                    Close(ref open, blocks);
                    open = new Block(MarkdownBlockKind.Bullet, Level(match.Groups[1].Value), null, match.Groups[2].Value);
                }
                else if ((match = Numbered.Match(line)).Success)
                {
                    Close(ref open, blocks);
                    open = new Block(MarkdownBlockKind.Numbered, Level(match.Groups[1].Value), match.Groups[2].Value + ".", match.Groups[3].Value);
                }
                else if (open != null)
                {
                    // A line that starts nothing new continues the open paragraph or list item.
                    open.Text.Append(' ').Append(line.Trim());
                }
                else
                {
                    open = new Block(MarkdownBlockKind.Paragraph, 0, null, line.Trim());
                }
            }

            Close(ref open, blocks);
            return blocks;
        }

        /// <summary>The spans of one line of text: bold (** or __), italic (*), inline code (`) and plain text.</summary>
        public static IReadOnlyList<MarkdownSpan> Inline(string text)
        {
            var spans = new List<MarkdownSpan>();
            AppendInline(text ?? string.Empty, MarkdownStyle.None, spans);
            return spans;
        }

        static void AppendInline(string text, MarkdownStyle style, List<MarkdownSpan> spans)
        {
            var plain = new StringBuilder();
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '\\' && i + 1 < text.Length && IsMarker(text[i + 1]))
                {
                    plain.Append(text[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '`' && TryClose(text, i, "`", out var codeEnd))
                {
                    Flush(plain, style, spans);
                    spans.Add(new MarkdownSpan(text.Substring(i + 1, codeEnd - i - 1), style | MarkdownStyle.Code));
                    i = codeEnd + 1;
                    continue;
                }

                var strong = (c == '*' || c == '_') && i + 1 < text.Length && text[i + 1] == c ? new string(c, 2) : null;
                if (strong != null && TryClose(text, i, strong, out var strongEnd))
                {
                    Flush(plain, style, spans);
                    AppendInline(text.Substring(i + 2, strongEnd - i - 2), style | MarkdownStyle.Bold, spans);
                    i = strongEnd + 2;
                    continue;
                }

                // Single underscores stay as written: they appear inside names like inspect_area.
                if (c == '*' && strong == null && TryClose(text, i, "*", out var emphasisEnd))
                {
                    Flush(plain, style, spans);
                    AppendInline(text.Substring(i + 1, emphasisEnd - i - 1), style | MarkdownStyle.Italic, spans);
                    i = emphasisEnd + 1;
                    continue;
                }

                plain.Append(c);
                i++;
            }

            Flush(plain, style, spans);
        }

        // An opening marker needs text right after it, and its closing marker text right before it: "a * b" is not
        // emphasis. A single * never closes on half of a **.
        static bool TryClose(string text, int open, string marker, out int close)
        {
            close = -1;
            var start = open + marker.Length;
            if (start >= text.Length || char.IsWhiteSpace(text[start]))
                return false;

            var code = marker == "`";
            for (var at = text.IndexOf(marker, start, StringComparison.Ordinal); at >= 0; at = text.IndexOf(marker, at + 1, StringComparison.Ordinal))
            {
                if (at == start)
                {
                    if (code)
                        return false;
                    continue;
                }

                if (!code && char.IsWhiteSpace(text[at - 1]))
                    continue;
                if (marker == "*" && (text[at - 1] == '*' || (at + 1 < text.Length && text[at + 1] == '*')))
                    continue;

                // "***" after "**bold *both" closes the italic first, then the bold: the bold ends the run.
                while (!code && marker.Length > 1 && at + marker.Length < text.Length && text[at + marker.Length] == marker[0])
                    at++;

                close = at;
                return true;
            }

            return false;
        }

        static bool IsMarker(char c) => c == '*' || c == '_' || c == '`' || c == '\\' || c == '#' || c == '-';

        // "---" or "***" on its own line is a divider, not an empty list item.
        static bool IsRule(string line)
        {
            var trimmed = line.Trim();
            return trimmed.Length >= 3 && (trimmed.Replace(" ", "").TrimStart('-').Length == 0 || trimmed.Replace(" ", "").TrimStart('*').Length == 0);
        }

        static int Level(string indent)
        {
            var width = 0;
            foreach (var c in indent)
                width += c == '\t' ? IndentPerLevel : 1;
            return width / IndentPerLevel;
        }

        static int ReadCode(string[] lines, int fence, List<MarkdownBlock> blocks)
        {
            var marker = Fence.Match(lines[fence]).Groups[1].Value;
            var body = new List<string>();
            var i = fence + 1;
            for (; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith(marker, StringComparison.Ordinal))
                    break;
                body.Add(lines[i]);
            }

            blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, 0, null,
                new[] { new MarkdownSpan(string.Join("\n", body), MarkdownStyle.Code) }));
            return i;
        }

        static void Flush(StringBuilder plain, MarkdownStyle style, List<MarkdownSpan> spans)
        {
            if (plain.Length == 0)
                return;
            spans.Add(new MarkdownSpan(plain.ToString(), style));
            plain.Clear();
        }

        static void Close(ref Block open, List<MarkdownBlock> blocks)
        {
            if (open == null)
                return;
            blocks.Add(new MarkdownBlock(open.Kind, open.Level, open.Marker, Inline(open.Text.ToString())));
            open = null;
        }

        sealed class Block
        {
            public Block(MarkdownBlockKind kind, int level, string marker, string text)
            {
                Kind = kind;
                Level = level;
                Marker = marker;
                Text = new StringBuilder(text.Trim());
            }

            public MarkdownBlockKind Kind { get; }

            public int Level { get; }

            public string Marker { get; }

            public StringBuilder Text { get; }
        }
    }
}
