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
