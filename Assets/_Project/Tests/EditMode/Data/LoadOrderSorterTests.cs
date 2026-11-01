using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Data
{
    public class LoadOrderSorterTests
    {
        static string[] SortedIds(params Plugin[] plugins) =>
            LoadOrderSorter.Sort(plugins).Select(p => p.Id).ToArray();

        [Test]
        public void MastersComeBeforeDependents()
        {
            var ids = SortedIds(
                Plugin("patch", new[] { "mod_a", "core" }),
                Plugin("mod_a", new[] { "core" }),
                Plugin("core"));

            Assert.That(ids, Is.EqualTo(new[] { "core", "mod_a", "patch" }));
        }

        [Test]
        public void IndependentPlugins_AreOrderedById()
        {
            var ids = SortedIds(
                Plugin("zeta", new[] { "core" }),
                Plugin("alpha", new[] { "core" }),
                Plugin("core"));

            Assert.That(ids, Is.EqualTo(new[] { "core", "alpha", "zeta" }));
        }

        [Test]
        public void MissingMaster_IsRejected()
        {
            Assert.Throws<DataValidationException>(() => SortedIds(Plugin("mod", new[] { "core" })));
        }

        [Test]
        public void Cycle_IsRejected()
        {
            var ex = Assert.Throws<DataValidationException>(() => SortedIds(
                Plugin("a", new[] { "b" }),
                Plugin("b", new[] { "a" })));

            StringAssert.Contains("circular", ex.Message);
        }

        [Test]
        public void DuplicateIds_AreRejected()
        {
            Assert.Throws<DataValidationException>(() => SortedIds(Plugin("core"), Plugin("core")));
        }

        [Test]
        public void SortedOrder_ResolvesCleanly()
        {
            var sorted = LoadOrderSorter.Sort(new[]
            {
                Plugin("mod", new[] { "core" }, Base("core:tree", "Big")),
                Plugin("core", Base("core:tree")),
            });

            Assert.DoesNotThrow(() => LoadOrderResolver.Resolve(sorted));
        }
    }
}
