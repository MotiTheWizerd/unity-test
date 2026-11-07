using System;
using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Plugins;

namespace Forge.Game.Items
{
    /// <summary>An item and how many of it are carried.</summary>
    public readonly struct ItemStack
    {
        public ItemStack(RecordId item, int count)
        {
            Item = item;
            Count = count;
        }

        /// <summary>The item: a base object with an item part.</summary>
        public RecordId Item { get; }

        public int Count { get; }

        public override string ToString() => $"{Count} × {Item}";
    }

    /// <summary>
    /// What an actor carries: a stack per item (by its base object's id) with how many, in the order first taken. One per
    /// actor: the player's now, the world's characters' and containers' later. What an item weighs or is worth is its
    /// record's, read when asked, so it is never stale. Engine-free.
    /// </summary>
    public sealed class Inventory
    {
        readonly List<ItemStack> _stacks = new List<ItemStack>();

        /// <summary>Something was added or removed.</summary>
        public event Action Changed;

        public IReadOnlyList<ItemStack> Stacks => _stacks;

        public int CountOf(RecordId item)
        {
            var index = IndexOf(item);
            return index < 0 ? 0 : _stacks[index].Count;
        }

        /// <summary>What it all weighs, by the items' records in <paramref name="records"/>; what is no item there weighs nothing.</summary>
        public float Weight(IRecordStore records)
        {
            var weight = 0f;
            foreach (var stack in _stacks)
            {
                if (ItemRecords.TryGet(records, stack.Item, out var item))
                    weight += item.Item.Weight * stack.Count;
            }

            return weight;
        }

        public void Add(RecordId item, int count = 1)
        {
            if (item.IsDefault)
                throw new ArgumentException("Name the item to add.", nameof(item));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Add at least one.");

            var index = IndexOf(item);
            if (index < 0)
                _stacks.Add(new ItemStack(item, count));
            else
                _stacks[index] = new ItemStack(item, checked(_stacks[index].Count + count));
            Changed?.Invoke();
        }

        /// <summary>Takes <paramref name="count"/> of <paramref name="item"/> out; false, and nothing taken, when fewer are carried.</summary>
        public bool TryRemove(RecordId item, int count = 1)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Remove at least one.");

            var index = IndexOf(item);
            if (index < 0 || _stacks[index].Count < count)
                return false;
            if (_stacks[index].Count == count)
                _stacks.RemoveAt(index);
            else
                _stacks[index] = new ItemStack(item, _stacks[index].Count - count);
            Changed?.Invoke();
            return true;
        }

        int IndexOf(RecordId item)
        {
            for (var i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Item == item)
                    return i;
            }

            return -1;
        }
    }
}
