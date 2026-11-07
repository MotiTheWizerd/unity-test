using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Attributes;
using Forge.Game.Equipment;
using Forge.Game.Events;
using Forge.Game.Items;
using NUnit.Framework;

namespace Forge.Tests.Game
{
    public class EquipmentTests
    {
        static readonly RecordId Right = CoreEquipSlots.RightHand;
        static readonly RecordId Left = CoreEquipSlots.LeftHand;
        static readonly RecordId Head = EquipSlotRecord.IdFor("forge.core", "head");
        static readonly RecordId Carry = AttributeRecord.IdFor("forge.core", "carry_weight");

        static readonly BaseObjectRecord Sword = Item("sword", ItemKind.Weapon, new EquippablePart(CoreEquipSlots.OneHanded()));
        static readonly BaseObjectRecord Axe = Item("axe", ItemKind.Weapon, new EquippablePart(CoreEquipSlots.TwoHanded()));
        static readonly BaseObjectRecord Ring = Item("ring", ItemKind.Misc,
            new EquippablePart(CoreEquipSlots.OneHanded(), new[] { AttributeModifier.Add(Carry, 50f) }));
        static readonly BaseObjectRecord Apple = Item("apple", ItemKind.Food, null);

        Inventory _inventory;
        GameEvents _events;
        ActorEquipment _equipment;
        List<IGameEvent> _told;

        [SetUp]
        public void SetUp()
        {
            _inventory = new Inventory();
            _events = new GameEvents();
            _equipment = new ActorEquipment(ActorId.Player, _inventory, _events);
            _told = new List<IGameEvent>();
            _events.SubscribeAll(_told.Add);
        }

        [TearDown]
        public void TearDown() => _equipment.Dispose();

        [Test]
        public void AOneHandedSword_GoesInTheRightHand_AndIsTold()
        {
            _inventory.Add(Sword.Id);
            var changed = 0;
            _equipment.Changed += () => changed++;

            Assert.That(_equipment.TryEquip(Sword), Is.EqualTo(EquipOutcome.Equipped));
            _events.Flush();

            Assert.That(_equipment.In(Right).Item, Is.EqualTo(Sword.Id));
            Assert.That(_equipment.In(Left), Is.Null);
            Assert.That(changed, Is.EqualTo(1));
            var told = _told.OfType<ItemEquipped>().Single();
            Assert.That((told.Actor, told.Item, told.Fit.Slots.Single()), Is.EqualTo((ActorId.Player, Sword.Id, Right)));
        }

        [Test]
        public void AShield_GoesInTheLeftHand_BesideASword()
        {
            var shield = Item("shield", ItemKind.Weapon, new EquippablePart(CoreEquipSlots.OffHand()));
            _inventory.Add(Sword.Id);
            _inventory.Add(shield.Id);

            Assert.That(_equipment.TryEquip(shield), Is.EqualTo(EquipOutcome.Equipped));
            Assert.That(_equipment.TryEquip(Sword), Is.EqualTo(EquipOutcome.Equipped));

            Assert.That((_equipment.In(Right).Item, _equipment.In(Left).Item), Is.EqualTo((Sword.Id, shield.Id)));
        }

        [Test]
        public void TwoSwords_AreHeldOneInEachHand_ButOneSwordOnlyOnce()
        {
            _inventory.Add(Sword.Id);
            _equipment.TryEquip(Sword);

            Assert.That(_equipment.TryEquip(Sword), Is.EqualTo(EquipOutcome.AlreadyEquipped), "One sword can't be in two hands.");

            _inventory.Add(Sword.Id);
            Assert.That(_equipment.TryEquip(Sword), Is.EqualTo(EquipOutcome.Equipped));

            Assert.That((_equipment.In(Right).Item, _equipment.In(Left).Item), Is.EqualTo((Sword.Id, Sword.Id)));
            Assert.That(_equipment.CountOf(Sword.Id), Is.EqualTo(2));
        }

        [Test]
        public void TheOnlySword_AskedIntoTheLeftHand_MovesThere()
        {
            _inventory.Add(Sword.Id);
            _equipment.TryEquip(Sword);

            Assert.That(_equipment.TryEquip(Sword, Left), Is.EqualTo(EquipOutcome.Equipped));
            _events.Flush();

            Assert.That(_equipment.In(Right), Is.Null);
            Assert.That(_equipment.In(Left).Item, Is.EqualTo(Sword.Id));
            Assert.That(_told.OfType<ItemUnequipped>().Single().Reason, Is.EqualTo(UnequipReason.Moved));
            Assert.That(_equipment.TryEquip(Sword, Left), Is.EqualTo(EquipOutcome.AlreadyEquipped));
        }

        [Test]
        public void ATwoHander_TakesBothHands_AndAOneHanderTakesItOffWhole()
        {
            _inventory.Add(Sword.Id, 2);
            _inventory.Add(Axe.Id);
            _equipment.TryEquip(Sword);
            _equipment.TryEquip(Sword);

            _equipment.TryEquip(Axe);
            _events.Flush();

            Assert.That(_equipment.In(Right), Is.SameAs(_equipment.In(Left)), "One axe in both hands.");
            Assert.That(_equipment.All.Single().Item, Is.EqualTo(Axe.Id));
            Assert.That(_told.OfType<ItemUnequipped>().Select(e => e.Reason), Is.EqualTo(new[] { UnequipReason.Replaced, UnequipReason.Replaced }));

            _equipment.TryEquip(Sword, Right);

            Assert.That(_equipment.In(Right).Item, Is.EqualTo(Sword.Id));
            Assert.That(_equipment.In(Left), Is.Null, "The axe came off both hands.");
        }

        [Test]
        public void WhatCannotBeEquipped_IsRefused_AndNothingChanges()
        {
            _inventory.Add(Apple.Id);
            _inventory.Add(Sword.Id);

            Assert.That(_equipment.TryEquip(Apple), Is.EqualTo(EquipOutcome.NotEquippable));
            Assert.That(_equipment.TryEquip(Axe), Is.EqualTo(EquipOutcome.NotCarried));
            Assert.That(_equipment.TryEquip(Sword, Head), Is.EqualTo(EquipOutcome.DoesNotFit));
            Assert.That(_equipment.All, Is.Empty);
        }

        [Test]
        public void DroppingOneOfTwoSwords_EmptiesTheLeftHand_AndTheLastBothHands()
        {
            _inventory.Add(Sword.Id, 2);
            _equipment.TryEquip(Sword);
            _equipment.TryEquip(Sword);

            _inventory.TryRemove(Sword.Id);
            _events.Flush();

            Assert.That(_equipment.In(Right).Item, Is.EqualTo(Sword.Id));
            Assert.That(_equipment.In(Left), Is.Null, "The left hand's goes first, as Skyrim does.");
            var told = _told.OfType<ItemUnequipped>().Single();
            Assert.That((told.Reason, told.Fit.Slots.Single()), Is.EqualTo((UnequipReason.NoLongerCarried, Left)));

            _inventory.Clear();

            Assert.That(_equipment.All, Is.Empty);
        }

        [Test]
        public void Unequip_TakesOffWhatTheSlotHolds()
        {
            _inventory.Add(Axe.Id);
            _equipment.TryEquip(Axe);

            Assert.That(_equipment.Unequip(Left), Is.True, "Either hand lets go of the axe.");
            Assert.That(_equipment.All, Is.Empty);
            Assert.That(_equipment.Unequip(Left), Is.False);
        }

        [Test]
        public void ARingsModifiers_ApplyWhileWorn_EachRingItsOwn()
        {
            var attributes = new ActorAttributes(AttributeSet.From(new[] { new AttributeRecord(Carry, "Carry Weight", AttributeKind.Stat, 100f) }), ActorId.Player);
            _inventory.Add(Ring.Id, 2);
            _equipment.TryEquip(Ring);
            using (new EquipmentModifiers(_equipment, attributes))
            {
                Assert.That(attributes.Value(Carry), Is.EqualTo(150f), "What was equipped before the binding counts too.");

                _equipment.TryEquip(Ring);
                Assert.That(attributes.Value(Carry), Is.EqualTo(200f));

                _equipment.Unequip(Right);
                Assert.That(attributes.Value(Carry), Is.EqualTo(150f), "Losing one ring leaves the other's.");
            }

            Assert.That(attributes.Value(Carry), Is.EqualTo(100f), "Unbound, the equipment does nothing.");
        }

        [Test]
        public void AModifierForAnAttributeTheWorldLacks_DoesNothing()
        {
            var attributes = new ActorAttributes(AttributeSet.From(new[] { new AttributeRecord(AttributeRecord.IdFor("forge.core", "health"), "Health", AttributeKind.Stat, 100f) }), ActorId.Player);
            _inventory.Add(Ring.Id);
            using (new EquipmentModifiers(_equipment, attributes))
                Assert.That(_equipment.TryEquip(Ring), Is.EqualTo(EquipOutcome.Equipped));
        }

        static BaseObjectRecord Item(string name, ItemKind kind, EquippablePart equippable) =>
            new BaseObjectRecord(new RecordId("forge.core", name), name, "items/" + name, item: new ItemPart(kind, 1f, 1), equippable: equippable);
    }
}
