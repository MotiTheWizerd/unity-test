using System;
using Forge.Data;
using Forge.Data.Records;

namespace Forge.Game.Equipment
{
    /// <summary>
    /// One thing an actor has equipped: which item, the record it was equipped as (records never change, so what it does
    /// is known until it comes off), and the way it is held (<see cref="Fit"/>, the slots it takes). Two swords in two
    /// hands are two of these.
    /// </summary>
    public sealed class EquippedItem
    {
        internal EquippedItem(BaseObjectRecord record, EquipFit fit)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Fit = fit ?? throw new ArgumentNullException(nameof(fit));
        }

        /// <summary>The item: its base object.</summary>
        public RecordId Item => Record.Id;

        public BaseObjectRecord Record { get; }

        /// <summary>The slots it takes: one of its <see cref="EquippablePart.Fits"/>.</summary>
        public EquipFit Fit { get; }

        public override string ToString() => $"{Item} in {string.Join(" + ", Fit.Slots)}";
    }

    /// <summary>What came of asking to equip something.</summary>
    public enum EquipOutcome
    {
        /// <summary>It is equipped now.</summary>
        Equipped,

        /// <summary>It already was, that way: nothing changed.</summary>
        AlreadyEquipped,

        /// <summary>It has no <see cref="EquippablePart"/>.</summary>
        NotEquippable,

        /// <summary>The actor carries none of it.</summary>
        NotCarried,

        /// <summary>None of its fits takes the slot asked for.</summary>
        DoesNotFit,
    }

    /// <summary>Why something came off.</summary>
    public enum UnequipReason
    {
        /// <summary>The actor took it off.</summary>
        Asked,

        /// <summary>Something else was equipped in its slot.</summary>
        Replaced,

        /// <summary>It went to another slot (the only sword, from the right hand to the left).</summary>
        Moved,

        /// <summary>The actor no longer carries it (dropped, sold, used up).</summary>
        NoLongerCarried,
    }
}
