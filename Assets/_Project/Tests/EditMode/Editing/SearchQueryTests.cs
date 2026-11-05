using Forge.Editing.Palette;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class SearchQueryTests
    {
        [Test]
        public void Terms_SplitOnWhitespace_AndABlankQueryHasNone()
        {
            Assert.That(SearchQuery.Terms("  stone \t wall "), Is.EqualTo(new[] { "stone", "wall" }));
            Assert.That(SearchQuery.Terms(null), Is.Empty);
            Assert.That(SearchQuery.Terms("   "), Is.Empty);
        }

        [Test]
        public void Matches_NeedsEveryTerm_InAnyField_IgnoringCase()
        {
            var terms = SearchQuery.Terms("STONE arch");

            Assert.That(SearchQuery.Matches(terms, "Stone Wall", "Architecture"), Is.True);
            Assert.That(SearchQuery.Matches(terms, "Stone Wall", "Nature"), Is.False);
            Assert.That(SearchQuery.Matches(terms, null, "Stone arch"), Is.True, "A missing field never matches, the others still do.");
            Assert.That(SearchQuery.Matches(SearchQuery.Terms(""), "anything"), Is.True);
        }
    }
}
