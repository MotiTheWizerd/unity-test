using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Events;
using Forge.Game.Items;

namespace Forge.Game.Equipment
{
    /// <summary>
    /// What one actor has equipped, the player's or anyone's: each equip slot holds one thing, and a thing takes the
    /// slots of one of its fits (<see cref="EquippablePart.Fits"/>). One-handed things go in either hand, so an actor
    /// holds one in each; a two-handed one takes both.
    /// <para>
    /// <see cref="TryEquip"/> takes the slot asked for, or else the first fit that is free, else the first fit; whatever
    /// was in those slots comes off. An actor equips no more of an item than it carries: with one sword, equipping it in
    /// the other hand moves it there. When the pack holds fewer than are equipped (one of two swords dropped), the
    /// extra comes off by itself, the one in the later fit first (the left hand before the right).
    /// </para>
    /// Every change is told twice: on <see cref="Equipped"/> and <see cref="Unequipped"/> as it happens, for what is
    /// bound to this actor (its attributes, its look), and on the game's stream (<see cref="ItemEquipped"/>,
    /// <see cref="ItemUnequipped"/>) for the rest of the world. Engine-free.
    /// </summary>
    public sealed class ActorEquipment : IDisposable
    {
        readonly Inventory _inventory;
        readonly GameEvents _events;
        readonly List<EquippedItem> _equipped = new List<EquippedItem>();

        /// <param name="inventory">What the actor carries: it equips only from it, and what leaves it comes off.</param>
        /// <param name="events">The game's stream, told what is equipped and comes off; null to tell no one.</param>
        public ActorEquipment(ActorId owner, Inventory inventory, GameEvents events = null)
        {
            if (owner.IsDefault)
                throw new ArgumentException("Equipment belongs to an actor.", nameof(owner));
            Owner = owner;
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _events = events;
            _inventory.Changed += OnInventoryChanged;
        }

        public ActorId Owner { get; }

        /// <summary>What is equipped, in the order it was.</summary>
        public IReadOnlyList<EquippedItem> All => _equipped;

        /// <summary>Something was equipped.</summary>
        public event Action<EquippedItem> Equipped;

        /// <summary>Something came off, and why.</summary>
        public event Action<EquippedItem, UnequipReason> Unequipped;

        /// <summary>What is equipped changed, for whatever shows it.</summary>
        public event Action Changed;

        /// <summary>What <paramref name="slot"/> holds, or null.</summary>
        public EquippedItem In(RecordId slot)
        {
            foreach (var equipped in _equipped)
            {
                if (equipped.Fit.Takes(slot))
                    return equipped;
            }

            return null;
        }

        /// <summary>How many of <paramref name="item"/> are equipped (two swords: 2).</summary>
        public int CountOf(RecordId item)
        {
            var count = 0;
            foreach (var equipped in _equipped)
            {
                if (equipped.Item == item)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Equips <paramref name="item"/>: in <paramref name="slot"/> when given (by the first fit that takes it), else
        /// in the first fit that is free, else the first. What was in those slots comes off.
        /// </summary>
        public EquipOutcome TryEquip(BaseObjectRecord item, RecordId? slot = null)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            var part = item.Equippable;
            if (part == null)
                return EquipOutcome.NotEquippable;
            var carried = _inventory.CountOf(item.Id);
            if (carried == 0)
                return EquipOutcome.NotCarried;

            var equipped = CountOf(item.Id);
            if (slot == null && equipped >= carried)
                return EquipOutcome.AlreadyEquipped;
            var fit = slot.HasValue ? FitTaking(part, slot.Value) : FreeFit(part) ?? part.Fits[0];
            if (fit == null)
                return EquipOutcome.DoesNotFit;
            if (Holds(item.Id, fit))
                return EquipOutcome.AlreadyEquipped;

            // With none to spare, one of its own copies elsewhere comes over; what fills the fit's slots comes off.
            if (equipped - OverlappingCopies(item.Id, fit) >= carried)
                Remove(LeastPreferred(item.Id, fit), UnequipReason.Moved);
            for (var i = _equipped.Count - 1; i >= 0; i--)
            {
                if (i < _equipped.Count && Overlaps(_equipped[i].Fit, fit))
                    Remove(_equipped[i], UnequipReason.Replaced);
            }

            var added = new EquippedItem(item, fit);
            _equipped.Add(added);
            Equipped?.Invoke(added);
            _events?.Publish(new ItemEquipped(Owner, added.Item, fit));
            Changed?.Invoke();
            return EquipOutcome.Equipped;
        }

        /// <summary>Takes off what <paramref name="slot"/> holds (all of it, every slot it takes); false when it held nothing.</summary>
        public bool Unequip(RecordId slot)
        {
            var equipped = In(slot);
            if (equipped == null)
                return false;
            Remove(equipped, UnequipReason.Asked);
            return true;
        }

        public void Dispose() => _inventory.Changed -= OnInventoryChanged;

        // What the pack no longer holds comes off: the least preferred copies first.
        void OnInventoryChanged()
        {
            for (var i = _equipped.Count - 1; i >= 0; i--)
            {
                if (i >= _equipped.Count)
                    continue;
                var item = _equipped[i].Item;
                while (CountOf(item) > _inventory.CountOf(item))
                    Remove(LeastPreferred(item, null), UnequipReason.NoLongerCarried);
            }
        }

        void Remove(EquippedItem equipped, UnequipReason reason)
        {
            if (!_equipped.Remove(equipped))
                return;
            Unequipped?.Invoke(equipped, reason);
            _events?.Publish(new ItemUnequipped(Owner, equipped.Item, equipped.Fit, reason));
            Changed?.Invoke();
        }

        bool Holds(RecordId item, EquipFit fit)
        {
            foreach (var equipped in _equipped)
            {
                if (equipped.Item == item && equipped.Fit.SameAs(fit))
                    return true;
            }

            return false;
        }

        int OverlappingCopies(RecordId item, EquipFit fit)
        {
            var count = 0;
            foreach (var equipped in _equipped)
            {
                if (equipped.Item == item && Overlaps(equipped.Fit, fit))
                    count++;
            }

            return count;
        }

        // Of the item's copies (outside the fit, when given), the one in its latest fit; the last equipped on a tie.
        EquippedItem LeastPreferred(RecordId item, EquipFit outside)
        {
            EquippedItem least = null;
            var leastRank = -1;
            foreach (var equipped in _equipped)
            {
                if (equipped.Item != item || (outside != null && Overlaps(equipped.Fit, outside)))
                    continue;
                var rank = RankOf(equipped);
                if (rank >= leastRank)
                {
                    least = equipped;
                    leastRank = rank;
                }
            }

            return least;
        }

        static int RankOf(EquippedItem equipped)
        {
            var fits = equipped.Record.Equippable.Fits;
            for (var i = 0; i < fits.Count; i++)
            {
                if (fits[i].SameAs(equipped.Fit))
                    return i;
            }

            return fits.Count;
        }

        static EquipFit FitTaking(EquippablePart part, RecordId slot)
        {
            foreach (var fit in part.Fits)
            {
                if (fit.Takes(slot))
                    return fit;
            }

            return null;
        }

        EquipFit FreeFit(EquippablePart part)
        {
            foreach (var fit in part.Fits)
            {
                var free = true;
                foreach (var slot in fit.Slots)
                    free &= In(slot) == null;
                if (free)
                    return fit;
            }

            return null;
        }

        static bool Overlaps(EquipFit a, EquipFit b)
        {
            foreach (var slot in a.Slots)
            {
                if (b.Takes(slot))
                    return true;
            }

            return false;
        }
    }
}
