using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Palette;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    public class ObjectPaletteTests
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
            Base("core:barrel", "Barrel", "Props/Containers"),
        });

        static string[] Names(ObjectPalette palette) => palette.Objects.Select(r => r.DisplayName).ToArray();

        [Test]
        public void Objects_AreOrderedByCategoryThenName_UncategorizedLast()
        {
            Assert.That(Names(Sample()), Is.EqualTo(new[] { "Boulder", "Oak Tree", "Pine Tree", "Barrel", "Crate", "Totem" }));
        }

        [Test]
        public void Groups_FollowTheSameOrder()
        {
            var groups = Sample().Groups;

            Assert.That(groups.Select(g => g.Category), Is.EqualTo(new[] { "Nature/Rocks", "Nature/Trees", "Props/Containers", "" }));
            Assert.That(groups[1].Objects.Select(r => r.DisplayName), Is.EqualTo(new[] { "Oak Tree", "Pine Tree" }));
            Assert.That(groups.SelectMany(g => g.Objects).Select(r => r.Id), Is.EqualTo(Sample().Objects.Select(r => r.Id)));
        }

        [Test]
        public void SameNames_AreOrderedById_SoTheOrderIsStable()
        {
            var palette = ObjectPalette.From(new[] { Base("core:b", "Rock"), Base("core:a", "Rock") });

            Assert.That(palette.Objects.Select(r => r.Id.ToString()), Is.EqualTo(new[] { "core:a", "core:b" }));
        }

        [Test]
        public void IndexOf_MatchesObjects()
        {
            var palette = Sample();

            Assert.That(palette.IndexOf(RecordId.Parse("core:oak")), Is.EqualTo(1));
            Assert.That(palette.IndexOf(RecordId.Parse("core:missing")), Is.EqualTo(-1));
            Assert.That(palette.Contains(RecordId.Parse("core:crate")), Is.True);
        }

        [TestCase("tree", new[] { "Oak Tree", "Pine Tree" })]
        [TestCase("TREE", new[] { "Oak Tree", "Pine Tree" })]
        [TestCase("nature", new[] { "Boulder", "Oak Tree", "Pine Tree" })]
        [TestCase("nature oak", new[] { "Oak Tree" })]
        [TestCase("core:bar", new[] { "Barrel" })]
        [TestCase("dragon", new string[0])]
        public void Filter_MatchesEveryTermInNameCategoryOrId(string query, string[] expected)
        {
            Assert.That(Names(Sample().Filter(query)), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Filter_BlankQuery_ReturnsThePalette(string query)
        {
            var palette = Sample();

            Assert.That(palette.Filter(query), Is.SameAs(palette));
        }

        [Test]
        public void Filter_DropsGroupsWithoutMatches()
        {
            var filtered = Sample().Filter("crate");

            Assert.That(filtered.Groups.Select(g => g.Category), Is.EqualTo(new[] { "Props/Containers" }));
        }

        [Test]
        public void Empty_HasNothing()
        {
            Assert.That(ObjectPalette.Empty.Count, Is.Zero);
            Assert.That(ObjectPalette.Empty.Groups, Is.Empty);
        }
    }
}
