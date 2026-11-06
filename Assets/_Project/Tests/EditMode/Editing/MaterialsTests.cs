using System.Linq;
using Forge.Data.Records;
using Forge.Editing;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class MaterialsTests
    {
        static MaterialRecord Material(string name, string displayName) =>
            new MaterialRecord(MaterialRecord.IdFor("core", name), displayName, "material/" + name);

        [Test]
        public void Materials_AreListedByName_AndSearchedByNameOrId()
        {
            var session = new Forge.Editing.EditSession(
                new[] { Plugin("core", Material("thatch", "Thatch"), Material("oak", "Oak Planks"), Material("iron", "dark iron")) },
                Plugin("user", new[] { "core" }));

            var all = Materials.Of(session.World.Records);

            Assert.That(all.Select(m => m.DisplayName), Is.EqualTo(new[] { "dark iron", "Oak Planks", "Thatch" }));
            Assert.That(Materials.Filter(all, "plank").Select(m => m.DisplayName), Is.EqualTo(new[] { "Oak Planks" }));
            Assert.That(Materials.Filter(all, "material/thatch").Single().DisplayName, Is.EqualTo("Thatch"));
            Assert.That(Materials.Filter(all, " "), Is.SameAs(all));
        }

        [Test]
        public void Kinds_AreTheCategoryHeads_OnceEach_ByName_AndOfKindPicksThem()
        {
            var all = new[]
            {
                new MaterialRecord(MaterialRecord.IdFor("core", "oak"), "Oak", "material/oak", "Wood"),
                new MaterialRecord(MaterialRecord.IdFor("core", "slate"), "Slate", "material/slate", "Stone/Rough"),
                new MaterialRecord(MaterialRecord.IdFor("core", "pine"), "Pine", "material/pine", "wood"),
                new MaterialRecord(MaterialRecord.IdFor("core", "odd"), "Odd", "material/odd"),
            };

            Assert.That(Materials.Kinds(all), Is.EqualTo(new[] { "Stone", "Wood" }), "Uncategorized gives no kind; case doesn't split one.");
            Assert.That(Materials.OfKind(all, "Wood").Select(m => m.DisplayName), Is.EqualTo(new[] { "Oak", "Pine" }));
            Assert.That(Materials.OfKind(all, "Stone").Single().DisplayName, Is.EqualTo("Slate"));
            Assert.That(Materials.OfKind(all, null), Is.SameAs(all));
        }

        [TestCase("Roof", "Roof")]
        [TestCase("WoodDark", "Wood Dark")]
        [TestCase("roof_tiles", "roof tiles")]
        [TestCase("Wall2Trim", "Wall2 Trim")]
        [TestCase("PBR", "PBR")]
        [TestCase("", "")]
        public void SlotLabel_SplitsWords(string slot, string label)
        {
            Assert.That(Materials.SlotLabel(slot), Is.EqualTo(label));
        }
    }
}
