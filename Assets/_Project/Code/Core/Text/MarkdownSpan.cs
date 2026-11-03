using System;

namespace Forge.Core.Text
{
    [Flags]
    public enum MarkdownStyle
    {
        None = 0,
        Bold = 1,
        Italic = 2,
        Code = 4,
    }

    /// <summary>A run of text in one style.</summary>
    public readonly struct MarkdownSpan
    {
        public MarkdownSpan(string text, MarkdownStyle style)
        {
            Text = text ?? string.Empty;
            Style = style;
        }

        public string Text { get; }

        public MarkdownStyle Style { get; }

        public override string ToString() => Style == MarkdownStyle.None ? Text : $"{Style}:{Text}";
    }
}
