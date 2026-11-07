using Forge.Data;
using Forge.Data.Records;
using Forge.Game.Actors;
using Forge.Game.Events;

namespace Forge.Game.Equipment
{
    /// <summary>An actor equipped an item, in the slots of <see cref="Fit"/> (a sword in the left hand).</summary>
    public readonly struct ItemEquipped : IGameEvent
    {
        public ItemEquipped(ActorId actor, RecordId item, EquipFit fit)
        {
            Actor = actor;
            Item = item;
            Fit = fit;
        }

        public ActorId Actor { get; }

        /// <summary>The item: its base object.</summary>
        public RecordId Item { get; }

        /// <summary>The slots it took.</summary>
        public EquipFit Fit { get; }

        public override string ToString() => $"{Actor}: equipped {Item} in {string.Join(" + ", Fit.Slots)}";
    }

    /// <summary>An item came off an actor, from the slots of <see cref="Fit"/>, and why.</summary>
    public readonly struct ItemUnequipped : IGameEvent
    {
        public ItemUnequipped(ActorId actor, RecordId item, EquipFit fit, UnequipReason reason)
        {
            Actor = actor;
            Item = item;
            Fit = fit;
            Reason = reason;
        }

        public ActorId Actor { get; }

        /// <summary>The item: its base object.</summary>
        public RecordId Item { get; }

        /// <summary>The slots it left.</summary>
        public EquipFit Fit { get; }

        public UnequipReason Reason { get; }

        public override string ToString() => $"{Actor}: unequipped {Item} from {string.Join(" + ", Fit.Slots)} ({Reason})";
    }
}
