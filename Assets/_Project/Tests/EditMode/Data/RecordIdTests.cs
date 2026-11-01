using System;
using System.Linq;
using Forge.Data;
using NUnit.Framework;

namespace Forge.Tests.Data
{
    public class RecordIdTests
    {
        [TestCase("forge.core:tree_oak", "forge.core", "tree_oak")]
        [TestCase("mymod:trees/oak-01", "mymod", "trees/oak-01")]
        [TestCase("a.b.c:x.y", "a.b.c", "x.y")]
        public void Parse_ValidId_SplitsNamespaceAndName(string text, string ns, string name)
        {
            var id = RecordId.Parse(text);

            Assert.That(id.Namespace, Is.EqualTo(ns));
            Assert.That(id.Name, Is.EqualTo(name));
            Assert.That(id.ToString(), Is.EqualTo(text));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("no_separator")]
        [TestCase(":name")]
        [TestCase("ns:")]
        [TestCase("a:b:c")]
        [TestCase("Forge.Core:tree")]
        [TestCase("forge:Tree")]
        [TestCase("forge core:tree")]
        [TestCase("forge..core:tree")]
        [TestCase("forge:trees//oak")]
        public void TryParse_InvalidId_Fails(string text)
        {
            Assert.That(RecordId.TryParse(text, out var id), Is.False);
            Assert.That(id.IsDefault, Is.True);
            Assert.Throws<FormatException>(() => RecordId.Parse(text));
        }

        [Test]
        public void Constructor_InvalidParts_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RecordId("Bad", "name"));
            Assert.Throws<ArgumentException>(() => new RecordId("ns", "Bad"));
            Assert.Throws<ArgumentException>(() => new RecordId(null, "name"));
        }

        [Test]
        public void Equality_IsByValue()
        {
            var a = RecordId.Parse("forge.core:tree");
            var b = new RecordId("forge.core", "tree");
            var c = RecordId.Parse("forge.core:rock");

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Default_IsDefaultAndNotEqualToAnyRealId()
        {
            RecordId none = default;

            Assert.That(none.IsDefault, Is.True);
            Assert.That(none.ToString(), Is.Empty);
            Assert.That(none, Is.Not.EqualTo(RecordId.Parse("a:b")));
        }

        [Test]
        public void CompareTo_OrdersByNamespaceThenName()
        {
            var ids = new[] { "b:a", "a:b", "a:a" }.Select(RecordId.Parse).ToList();

            ids.Sort();

            Assert.That(ids.Select(i => i.ToString()), Is.EqualTo(new[] { "a:a", "a:b", "b:a" }));
        }
    }
}
