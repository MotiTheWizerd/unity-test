using System;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Game.Equipment;
using Forge.Game.Items;

namespace Forge.Game.Actors
{
    /// <summary>
    /// Gives an actor what its record says it starts with (<see cref="ActorRecord.Items"/>): the items into its pack, then
    /// the copies named for a slot equipped there. Equipped, not drawn: weapons start sheathed. What the world has no item
    /// for (its plugin unloaded, a mod took it away) is left out, and so is an equip that doesn't fit. Engine-free.
    /// </summary>
    public static class StartingItems
    {
        public static void Give(ActorRecord actor, IRecordStore records, Inventory inventory, ActorEquipment equipment)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (equipment == null)
                throw new ArgumentNullException(nameof(equipment));

            foreach (var entry in actor.Items)
            {
                if (!ItemRecords.TryGet(records, entry.Item, out var item))
                    continue;
                inventory.Add(item.Id, entry.Count);
                if (entry.Equip == null)
                    continue;
                foreach (var slot in entry.Equip)
                    equipment.TryEquip(item, slot);
            }
        }
    }
}
