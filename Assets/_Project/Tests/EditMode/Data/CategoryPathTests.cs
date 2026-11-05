using System;
using Forge.Data.Records;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class CategoryPathTests
    {
        [TestCase(" Nature / Trees ", "Nature/Trees")]
        [TestCase("Nature", "Nature")]
        [TestCase("", "")]
        [TestCase("   ", "")]
        [TestCase(null, "")]
        public void Normalize_TrimsSegments_BlankIsEmpty(string path, string expected)
        {
            Assert.That(CategoryPath.Normalize(path), Is.EqualTo(expected));
        }

        [Test]
        public void Normalize_RejectsEmptySegments()
        {
            Assert.Throws<ArgumentException>(() => CategoryPath.Normalize("Nature//Trees"));
        }

        [TestCase("Nature/Trees", "Nature", "Trees")]
        [TestCase("Architecture/Interior/Kitchen", "Architecture", "Interior/Kitchen")]
        [TestCase("Props", "Props", "")]
        [TestCase("", "", "")]
        public void Head_AndTail_SplitAtTheFirstSeparator(string path, string head, string tail)
        {
            Assert.That(CategoryPath.Head(path), Is.EqualTo(head));
            Assert.That(CategoryPath.Tail(path), Is.EqualTo(tail));
        }
    }
}
