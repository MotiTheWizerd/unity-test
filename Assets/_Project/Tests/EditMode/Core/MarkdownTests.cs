using System.Linq;
using Forge.Core.Text;
using NUnit.Framework;

namespace Forge.Tests.Core
{
    public class MarkdownTests
    {
        static string Spans(MarkdownBlock block) => string.Join(" | ", block.Spans.Select(s => s.ToString()));

        [Test]
        public void TheAgentsReply_SplitsIntoAParagraphAndListItems()
        {
            var blocks = Markdown.Parse(
                "I made three changes:\n" +
                "- **Bench:** It now faces north.\n" +
                "- **House:** I scaled it up.\n" +
                "\n" +
                "I didn't run a check.");

            Assert.That(blocks.Select(b => b.Kind), Is.EqualTo(new[]
            {
                MarkdownBlockKind.Paragraph, MarkdownBlockKind.Bullet, MarkdownBlockKind.Bullet, MarkdownBlockKind.Paragraph,
            }));
            Assert.That(Spans(blocks[1]), Is.EqualTo("Bold:Bench: |  It now faces north."));
        }

        [Test]
        public void LinesOfAParagraph_AreJoinedWithSpaces_AndABlankLineEndsIt()
        {
            var blocks = Markdown.Parse("one\ntwo\r\n\r\nthree");

            Assert.That(blocks.Select(Spans), Is.EqualTo(new[] { "one two", "three" }));
        }

        [Test]
        public void ListItems_NestByIndent_AndContinueOnTheirNextLine()
        {
            var blocks = Markdown.Parse("1. first\n   still first\n  - inner\n2) second");

            Assert.That(blocks.Select(b => (b.Kind, b.Level, b.Marker, Spans(b))), Is.EqualTo(new[]
            {
                (MarkdownBlockKind.Numbered, 0, "1.", "first still first"),
                (MarkdownBlockKind.Bullet, 1, (string)null, "inner"),
                (MarkdownBlockKind.Numbered, 0, "2.", "second"),
            }));
        }

        [Test]
        public void Headings_CodeFences_AndRules()
        {
            var blocks = Markdown.Parse("## Plan ##\n```\nx = 1\n  y\n```\n---\nend");

            Assert.That(blocks.Select(b => b.Kind), Is.EqualTo(new[] { MarkdownBlockKind.Heading, MarkdownBlockKind.Code, MarkdownBlockKind.Paragraph }));
            Assert.That((blocks[0].Level, Spans(blocks[0])), Is.EqualTo((1, "Plan")));
            Assert.That(blocks[1].Spans.Single().Text, Is.EqualTo("x = 1\n  y"), "Code keeps its lines and spacing.");
        }

        [TestCase("**bold** and *italic*", "Bold:bold |  and  | Italic:italic")]
        [TestCase("__bold__", "Bold:bold")]
        [TestCase("**bold *both***", "Bold:bold  | Bold, Italic:both")]
        [TestCase("call `inspect_area` first", "call  | Code:inspect_area |  first")]
        [TestCase("inspect_area and place_line", "inspect_area and place_line")]
        [TestCase("2 * 3 * 4", "2 * 3 * 4")]
        [TestCase("**not closed", "**not closed")]
        [TestCase(@"\*literal\*", "*literal*")]
        [TestCase("a <b>tag</b>", "a <b>tag</b>")]
        public void InlineStyles(string text, string expected)
        {
            Assert.That(string.Join(" | ", Markdown.Inline(text).Select(s => s.ToString())), Is.EqualTo(expected));
        }

        [Test]
        public void EmptyText_HasNoBlocks()
        {
            Assert.That(Markdown.Parse(""), Is.Empty);
            Assert.That(Markdown.Parse(null), Is.Empty);
            Assert.That(Markdown.Parse("\n \n"), Is.Empty);
        }
    }
}
