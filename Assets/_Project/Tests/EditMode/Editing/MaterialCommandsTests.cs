using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing.Building;
using Forge.Editing.Commands;
using NUnit.Framework;

namespace Forge.Tests.Editing
{
    /// <summary>Materials through the agent's commands, over <see cref="BuildingFixture"/> (the house has Plaster and Roof slots).</summary>
    public class MaterialCommandsTests
    {
        static readonly RecordId Thatch = MaterialRecord.IdFor("core", "thatch");
        static readonly RecordId Brick = MaterialRecord.IdFor("core", "brick");
        static readonly RecordId HouseA = RecordId.Parse("core:ref/house_a");
        static readonly RecordId HouseB = RecordId.Parse("core:ref/house_b");
        static readonly RecordId Pine = RecordId.Parse("core:ref/pine");

        static BuildingFixture World() => BuildingFixture.Create(
            new MaterialRecord(Thatch, "Thatch", "material/thatch"),
            new MaterialRecord(Brick, "Red Brick", "material/brick"),
            Placed(HouseA, BuildingFixture.House, 20f),
            Placed(HouseB, BuildingFixture.House, 40f),
            Placed(Pine, BuildingFixture.Tree, 60f));

        static PlacedObjectRecord Placed(RecordId id, RecordId baseId, float x) =>
            new PlacedObjectRecord(id, baseId, BuildingFixture.Space, new Vector3(x, 12f, 20f), Quaternion.Identity, 1f);

        static SetMaterialsCommand.Result Set(BuildingFixture world, string slot, string material, params RecordId[] ids) =>
            new SetMaterialsCommand(world.Context).Execute(new SetMaterialsCommand.Arguments
            {
                Ids = ids.Select(id => id.ToString()).ToList(),
                Slot = slot,
                Material = material,
            });

        static RecordId? Wears(BuildingFixture world, RecordId id, string slot) =>
            world.Session.World.Records.Get<PlacedObjectRecord>(id).MaterialIn(slot);

        [Test]
        public void ListMaterials_NamesThem_AndSearches()
        {
            var command = new ListMaterialsCommand(World().Context);

            var all = command.Execute(new ListMaterialsCommand.Arguments());
            var brick = command.Execute(new ListMaterialsCommand.Arguments { Search = "brick" });

            Assert.That(all.Materials.Select(m => m.Name), Is.EqualTo(new[] { "Red Brick", "Thatch" }));
            Assert.That(brick.Materials.Single().Id, Is.EqualTo(Brick.ToString()));
            StringAssert.Contains("1 of 2", brick.Summary);
        }

        [Test]
        public void ListObjects_NamesEachObjectsSlots()
        {
            var objects = new ListObjectsCommand(World().Context).Execute(new ListObjectsCommand.Arguments()).Objects;

            Assert.That(objects.Single(o => o.Id == BuildingFixture.House.ToString()).Slots, Is.EqualTo(new[] { "Plaster", "Roof" }));
            Assert.That(objects.Single(o => o.Id == BuildingFixture.Tree.ToString()).Slots, Is.Empty);
        }

        [Test]
        public void SetMaterials_DressesTheSlot_InOneUndoStep_SkippingObjectsWithoutIt()
        {
            var world = World();

            var result = Set(world, "Roof", Thatch.ToString(), HouseA, HouseB, Pine);

            Assert.That(result.Changed, Is.EqualTo(2));
            Assert.That(Wears(world, HouseA, "Roof"), Is.EqualTo(Thatch));
            Assert.That(Wears(world, HouseB, "Roof"), Is.EqualTo(Thatch));
            Assert.That(result.Skipped.Single().Id, Is.EqualTo(Pine.ToString()));
            StringAssert.Contains("no material slots", result.Skipped.Single().Reason);

            world.Session.Undo();
            Assert.That(Wears(world, HouseA, "Roof"), Is.Null);
        }

        [Test]
        public void SetMaterials_WithoutAMaterial_GivesTheSlotBackItsOwnLook()
        {
            var world = World();
            Set(world, "Plaster", Brick.ToString(), HouseA);

            var result = Set(world, "Plaster", null, HouseA);

            Assert.That(result.Changed, Is.EqualTo(1));
            Assert.That(Wears(world, HouseA, "Plaster"), Is.Null);
            StringAssert.Contains("its own look", result.Summary);
        }

        [Test]
        public void SetMaterials_RefusesUnknownMaterials_AndSlotsNoObjectHas_WithHints()
        {
            var world = World();

            var unknown = Assert.Throws<CommandException>(() => Set(world, "Roof", "core:material/lava", HouseA));
            var noSlot = Assert.Throws<CommandException>(() => Set(world, "Chimney", Thatch.ToString(), HouseA));

            StringAssert.Contains("list_materials", unknown.Message);
            StringAssert.Contains("\"Plaster\", \"Roof\"", noSlot.Message);
            Assert.That(world.Session.CanUndo, Is.False);
        }

        [Test]
        public void InspectedObjects_ShowWhatTheyWear()
        {
            var world = World();
            Set(world, "Roof", Thatch.ToString(), HouseA);

            var info = new PlacedObjectInfo(world.Session.World.Records.Get<PlacedObjectRecord>(HouseA), "Small House");

            Assert.That(info.Materials, Is.EqualTo(new Dictionary<string, string> { { "Roof", Thatch.ToString() } }));
            Assert.That(new PlacedObjectInfo(world.Session.World.Records.Get<PlacedObjectRecord>(HouseB), "Small House").Materials, Is.Null);
        }
    }
}
