using System.Collections.Generic;
using System.Text;
using Forge.Core.Text;
using UnityEngine.UIElements;

namespace Forge.EditorApp.UI
{
    /// <summary>
    /// Shows Markdown text (<see cref="Markdown"/>) as UI Toolkit elements: one selectable label per paragraph,
    /// heading or code block, and list items as a marker beside their text, indented by nesting level. Inline
    /// styles become rich text; the text itself is never read as rich-text tags.
    /// </summary>
    public static class MarkdownView
    {
        const string Root = "forge-md";
        const string Block = Root + "__block";
        const string Spaced = Block + "--spaced";
        const string ItemSpaced = Block + "--item-spaced";
        const string CodeColor = "#A9D3FF";

        public static VisualElement Build(string text)
        {
            var root = new VisualElement();
            root.AddToClassList(Root);

            MarkdownBlock previous = null;
            foreach (var block in Markdown.Parse(text))
            {
                var element = Element(block);
                element.AddToClassList(Block);
                if (previous != null)
                    element.AddToClassList(IsItem(block) && IsItem(previous) ? ItemSpaced : Spaced);
                root.Add(element);
                previous = block;
            }

            return root;
        }

        static VisualElement Element(MarkdownBlock block)
        {
            switch (block.Kind)
            {
                case MarkdownBlockKind.Bullet:
                case MarkdownBlockKind.Numbered:
                    var item = new VisualElement();
                    item.AddToClassList(Root + "__item");
                    item.style.marginLeft = block.Level * 12;
                    var marker = new Label(block.Kind == MarkdownBlockKind.Bullet ? "•" : block.Marker) { enableRichText = false };
                    marker.AddToClassList(Root + "__marker");
                    item.Add(marker);
                    var body = TextLabel(block.Spans);
                    body.AddToClassList(Root + "__item-text");
                    item.Add(body);
                    return item;
                case MarkdownBlockKind.Heading:
                    return WithClass(TextLabel(block.Spans), Root + "__heading");
                case MarkdownBlockKind.Code:
                    return WithClass(TextLabel(block.Spans, colorCode: false), Root + "__code");
                default:
                    return TextLabel(block.Spans);
            }
        }

        static Label TextLabel(IReadOnlyList<MarkdownSpan> spans, bool colorCode = true)
        {
            var label = new Label(RichText(spans, colorCode)) { enableRichText = true };
            label.selection.isSelectable = true;
            return label;
        }

        static string RichText(IReadOnlyList<MarkdownSpan> spans, bool colorCode)
        {
            var text = new StringBuilder();
            foreach (var span in spans)
            {
                var bold = (span.Style & MarkdownStyle.Bold) != 0;
                var italic = (span.Style & MarkdownStyle.Italic) != 0;
                var code = colorCode && (span.Style & MarkdownStyle.Code) != 0;
                if (bold) text.Append("<b>");
                if (italic) text.Append("<i>");
                if (code) text.Append("<color=").Append(CodeColor).Append('>');
                // noparse keeps any '<' in the words themselves from being read as a tag.
                text.Append("<noparse>").Append(span.Text.Replace("</noparse>", "</ noparse>")).Append("</noparse>");
                if (code) text.Append("</color>");
                if (italic) text.Append("</i>");
                if (bold) text.Append("</b>");
            }

            return text.ToString();
        }

        static bool IsItem(MarkdownBlock block) => block.Kind == MarkdownBlockKind.Bullet || block.Kind == MarkdownBlockKind.Numbered;

        static VisualElement WithClass(VisualElement element, string className)
        {
            element.AddToClassList(className);
            return element;
        }
    }
}
