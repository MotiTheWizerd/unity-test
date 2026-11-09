using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Equipment;
using Forge.Game.Hud;
using Forge.Game.Items;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Game
{
    public class AmmoCountTests
    {
        static readonly RecordId Archery = CombatStyleRecord.IdFor("forge.core", "archery");
        static readonly RecordId OneHanded = CombatStyleRecord.IdFor("forge.core", "one_handed");

        static readonly BaseObjectRecord Bow = new BaseObjectRecord(new RecordId("forge.core", "bow"), "Bow", "items/bow",
            item: new ItemPart(ItemKind.Weapon, 3f, 50), equippable: new EquippablePart(CoreEquipSlots.Bow()),
            weapon: new WeaponPart(Archery, new[] { Effect.Damage(CoreAttributes.Health, 10f) }, CoreEquipSlots.Ammo));

        static readonly BaseObjectRecord Sword = new BaseObjectRecord(new RecordId("forge.core", "sword"), "Sword", "items/sword",
            item: new ItemPart(ItemKind.Weapon, 9f, 75), equippable: new EquippablePart(CoreEquipSlots.OneHanded()),
            weapon: new WeaponPart(OneHanded, new[] { Effect.Damage(CoreAttributes.Health, 12f) }));

        static readonly BaseObjectRecord Arrow = new BaseObjectRecord(new RecordId("forge.core", "iron_arrow"), "Iron Arrow", "items/arrow",
            item: new ItemPart(ItemKind.Misc, 0.1f, 1), equippable: new EquippablePart(CoreEquipSlots.Ammunition()), ammo: new AmmoPart(0.2f));

        IRecordStore _world;
        Inventory _inventory;
        ActorEquipment _equipment;
        WeaponReadiness _readiness;

        [SetUp]
        public void SetUp()
        {
            _world = LoadOrderResolver.Resolve(new[]
            {
                Plugin("forge.core",
                    new EquipSlotRecord(CoreEquipSlots.RightHand, "Right Hand", "RightHand", sheathes: true),
                    new EquipSlotRecord(CoreEquipSlots.LeftHand, "Left Hand", "LeftHand", sheathes: true),
                    new EquipSlotRecord(CoreEquipSlots.Ammo, "Ammo", "Chest"),
                    Bow,
                    Sword,
                    Arrow),
            }).Records;
            _inventory = new Inventory();
            _equipment = new ActorEquipment(ActorId.Player, _inventory);
            _readiness = new WeaponReadiness(_equipment, _world);
            _readiness.SetTimes(0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            _readiness.Dispose();
            _equipment.Dispose();
        }

        AmmoCount Read() => AmmoCount.Of(_equipment, _inventory, _readiness, _world);

        void Hold(BaseObjectRecord item, int count = 1)
        {
            _inventory.Add(item.Id, count);
            _equipment.TryEquip(item);
        }

        void ToggleWeapons()
        {
            _readiness.Toggle();
            _readiness.Tick(0f);
        }

        [Test]
        public void ABowDrawn_ShowsItsArrows_AndHowManyAreCarried()
        {
            Hold(Bow);
            Hold(Arrow, 24);
            Assert.That(Read(), Is.EqualTo(AmmoCount.Hidden), "Sheathed.");

            ToggleWeapons();
            Assert.That(Read(), Is.EqualTo(new AmmoCount("Iron Arrow", 24)));

            _equipment.TryUse(CoreEquipSlots.Ammo, out _);
            Assert.That(Read().Count, Is.EqualTo(23), "One shot.");

            ToggleWeapons();
            Assert.That(Read(), Is.EqualTo(AmmoCount.Hidden), "Put away.");
        }

        [Test]
        public void ABowDrawn_WithNoArrows_ShowsNoneOfItsSlot()
        {
            Hold(Bow);
            Hold(Arrow, 1);
            ToggleWeapons();

            _equipment.TryUse(CoreEquipSlots.Ammo, out _);

            Assert.That(Read(), Is.EqualTo(new AmmoCount("Ammo", 0)), "The last came off as it was used.");
        }

        [Test]
        public void ASwordDrawn_ShowsNoCount()
        {
            Hold(Sword);
            Hold(Arrow, 5);
            ToggleWeapons();

            Assert.That(Read().Shown, Is.False);
        }
    }
}
