using System;
using System.Collections.Generic;

namespace Forge.Core.Collections
{
    /// <summary>
    /// A binary min-heap of items by a float priority: the lowest comes out first. Ties come out in no set order.
    /// (The runtime's PriorityQueue isn't available to Unity's .NET profile.)
    /// </summary>
    public sealed class MinHeap<T>
    {
        readonly List<(float Priority, T Item)> _entries = new List<(float, T)>();

        public int Count => _entries.Count;

        public void Push(float priority, T item)
        {
            if (float.IsNaN(priority))
                throw new ArgumentOutOfRangeException(nameof(priority), "A priority can't be NaN.");

            _entries.Add((priority, item));
            var child = _entries.Count - 1;
            while (child > 0)
            {
                var parent = (child - 1) / 2;
                if (_entries[parent].Priority <= _entries[child].Priority)
                    break;
                Swap(parent, child);
                child = parent;
            }
        }

        /// <summary>Removes and returns the item with the lowest priority.</summary>
        public (float Priority, T Item) Pop()
        {
            if (_entries.Count == 0)
                throw new InvalidOperationException("The heap is empty.");

            var top = _entries[0];
            var last = _entries.Count - 1;
            _entries[0] = _entries[last];
            _entries.RemoveAt(last);

            var parent = 0;
            while (true)
            {
                var smallest = parent;
                var left = 2 * parent + 1;
                var right = left + 1;
                if (left < _entries.Count && _entries[left].Priority < _entries[smallest].Priority)
                    smallest = left;
                if (right < _entries.Count && _entries[right].Priority < _entries[smallest].Priority)
                    smallest = right;
                if (smallest == parent)
                    break;
                Swap(parent, smallest);
                parent = smallest;
            }

            return top;
        }

        void Swap(int a, int b) => (_entries[a], _entries[b]) = (_entries[b], _entries[a]);
    }
}
