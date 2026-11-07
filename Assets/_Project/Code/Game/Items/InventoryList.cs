using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Game.Items
{
    /// <summary>One line of an inventory as a screen lists it: the item, how many, and what one weighs and is worth.</summary>
    public readonly struct InventoryEntry
    {
        public InventoryEntry(BaseObjectRecord item, int count)
        {
            Record = item ?? throw new ArgumentNullException(nameof(item));
            Count = count;
        }

        /// <summary>The item's base object, its <see cref="BaseObjectRecord.Item"/> part set.</summary>
        public BaseObjectRecord Record { get; }

        public RecordId Item => Record.Id;

        public string Name => Record.DisplayName;

        public ItemKind Kind => Record.Item.Kind;

        public int Count { get; }

        /// <summary>What one weighs.</summary>
        public float Weight => Record.Item.Weight;

        /// <summary>What one is worth, in gold.</summary>
        public int Value => Record.Item.Value;

        /// <summary>What all of them weigh.</summary>
        public float TotalWeight => Weight * Count;
    }

    /// <summary>
    /// An inventory as a screen lists it, as Skyrim's: by name, every kind together or one kind (a tab), each item read
    /// from the world's records so its name, weight and value are always the current ones. Engine-free.
    /// </summary>
    public static class InventoryList
    {
        /// <summary>
        /// What <paramref name="inventory"/> holds, of <paramref name="kind"/> only when given, sorted by name. Items the
        /// world has no record of are left out (they stay carried, and show again when their plugin is back).
        /// </summary>
        public static IReadOnlyList<InventoryEntry> Of(Inventory inventory, IRecordStore records, ItemKind? kind = null)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (records == null)
                throw new ArgumentNullException(nameof(records));

            var entries = new List<InventoryEntry>(inventory.Stacks.Count);
            foreach (var stack in inventory.Stacks)
            {
                if (ItemRecords.TryGet(records, stack.Item, out var item) && (kind == null || item.Item.Kind == kind))
                    entries.Add(new InventoryEntry(item, stack.Count));
            }

            entries.Sort(ByName);
            return entries;
        }

        static int ByName(InventoryEntry a, InventoryEntry b)
        {
            var byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : a.Item.CompareTo(b.Item);
        }
    }
}
