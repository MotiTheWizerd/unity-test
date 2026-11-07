using System;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Equipment;
using Forge.Game.Items;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Game
{
    public class StartingItemsTests
    {
        static readonly RecordId Sword = RecordId.Parse("core:sword");
        static readonly RecordId Apple = RecordId.Parse("core:apple");
        static readonly RecordId Right = CoreEquipSlots.RightHand;
        static readonly RecordId Left = CoreEquipSlots.LeftHand;
        static readonly RecordId Hero = ActorRecord.IdFor("core", "hero");

        static IRecordStore World() => LoadOrderResolver.Resolve(new[]
        {
            Plugin("core",
                new BaseObjectRecord(Sword, "Sword", "items/sword", item: new ItemPart(ItemKind.Weapon, 9f, 75),
                    equippable: new EquippablePart(CoreEquipSlots.OneHanded())),
                new BaseObjectRecord(Apple, "Apple", "items/apple", item: new ItemPart(ItemKind.Food, 0.5f, 3))),
        }).Records;

        Inventory _inventory;
        ActorEquipment _equipment;

        [SetUp]
        public void SetUp()
        {
            _inventory = new Inventory();
            _equipment = new ActorEquipment(ActorId.Player, _inventory);
        }

        [TearDown]
        public void TearDown() => _equipment.Dispose();

        [Test]
        public void AnActor_StartsCarryingItsItems_WithTheOnesNamedForASlotEquippedThere()
        {
            var hero = new ActorRecord(Hero, "Hero", new[] { new ActorItem(Sword, equip: new[] { Right }), new ActorItem(Apple, 3) });

            StartingItems.Give(hero, World(), _inventory, _equipment);

            Assert.That((_inventory.CountOf(Sword), _inventory.CountOf(Apple)), Is.EqualTo((1, 3)));
            Assert.That(_equipment.In(Right)?.Item, Is.EqualTo(Sword));
            Assert.That(_equipment.In(Left), Is.Null);
        }

        [Test]
        public void TwoSwords_CanStartOneInEachHand()
        {
            var hero = new ActorRecord(Hero, "Hero", new[] { new ActorItem(Sword, 2, new[] { Right, Left }) });

            StartingItems.Give(hero, World(), _inventory, _equipment);

            Assert.That(_equipment.In(Right)?.Item, Is.EqualTo(Sword));
            Assert.That(_equipment.In(Left)?.Item, Is.EqualTo(Sword));
        }

        [Test]
        public void WhatTheWorldHasNoItemFor_IsLeftOut_AndSoIsAnEquipThatDoesNotFit()
        {
            var hero = new ActorRecord(Hero, "Hero", new[]
            {
                new ActorItem(RecordId.Parse("gone:axe"), equip: new[] { Right }),
                new ActorItem(Apple, equip: new[] { Right }),
            });

            StartingItems.Give(hero, World(), _inventory, _equipment);

            Assert.That(_inventory.CountOf(RecordId.Parse("gone:axe")), Is.Zero);
            Assert.That(_inventory.CountOf(Apple), Is.EqualTo(1), "Still carried, just not held.");
            Assert.That(_equipment.All, Is.Empty);
        }

        [Test]
        public void AnActorRecord_ListsEachItemOnce_AndEquipsNoMoreThanItCarries()
        {
            Assert.Throws<ArgumentException>(() => new ActorRecord(Hero, "Hero", new[] { new ActorItem(Apple), new ActorItem(Apple) }));
            Assert.Throws<ArgumentException>(() => new ActorItem(Sword, 1, new[] { Right, Left }));
            Assert.Throws<ArgumentException>(() => new ActorItem(Sword, 2, new[] { Right, Right }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActorItem(Sword, 0));
            Assert.Throws<ArgumentException>(() => new ActorRecord(RecordId.Parse("core:hero"), "Hero"));
            Assert.That(new ActorRecord(Hero, "Hero").Items, Is.Empty);
        }
    }
}
