using System;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Palette;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class PaletteTabsTests
    {
        static BaseObjectRecord Base(string id, string displayName, string category = null) =>
            new BaseObjectRecord(RecordId.Parse(id), displayName, "primitive/cube", category);

        static ObjectPalette Sample() => ObjectPalette.From(new[]
        {
            Base("core:totem", "Totem"),
            Base("core:pine", "Pine Tree", "Nature/Trees"),
            Base("core:crate", "Crate", "Props/Containers"),
            Base("core:oak", "Oak Tree", "Nature/Trees"),
            Base("core:boulder", "Boulder", "Nature/Rocks"),
            Base("core:lamp", "Lamp", "Props"),
            Base("core:barrel", "Barrel", "Props/Containers"),
        });

        [Test]
        public void From_OneTabPerTopLevelCategory_InGroupOrder_UncategorizedLast()
        {
            var tabs = PaletteTabs.From(Sample());

            Assert.That(tabs.Select(t => t.Category), Is.EqualTo(new[] { "Nature", "Props", "" }));
            Assert.That(tabs.Select(t => t.Count), Is.EqualTo(new[] { 3, 3, 1 }));
        }

        [Test]
        public void From_TabsKeepTheirGroupsInDisplayOrder()
        {
            var tabs = PaletteTabs.From(Sample());

            Assert.That(tabs[0].Groups.Select(g => g.Category), Is.EqualTo(new[] { "Nature/Rocks", "Nature/Trees" }));
            Assert.That(tabs[1].Groups.Select(g => g.Category), Is.EqualTo(new[] { "Props", "Props/Containers" }));
            Assert.That(tabs[2].Groups.Select(g => g.Category), Is.EqualTo(new[] { "" }));
        }

        [Test]
        public void From_EmptyPalette_HasNoTabs()
        {
            Assert.That(PaletteTabs.From(ObjectPalette.Empty), Is.Empty);
        }

        [Test]
        public void From_FilteredPalette_OnlyListsTabsWithMatches()
        {
            var tabs = PaletteTabs.From(Sample().Filter("tree"));

            Assert.That(tabs.Select(t => t.Category), Is.EqualTo(new[] { "Nature" }));
            Assert.That(tabs[0].Groups.Select(g => g.Category), Is.EqualTo(new[] { "Nature/Trees" }));
        }

        [Test]
        public void Tab_RejectsGroupsOutsideItsCategory()
        {
            var group = Sample().Groups.First(g => g.Category == "Props/Containers");

            Assert.Throws<ArgumentException>(() => new PaletteTab("Nature", new[] { group }));
            Assert.Throws<ArgumentException>(() => new PaletteTab("Nature", Array.Empty<PaletteGroup>()));
        }
    }
}
