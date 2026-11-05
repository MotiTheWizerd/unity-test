using System;
using System.Collections.Generic;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// The decoded heights of stored chunks, kept up to a memory budget: when it is exceeded, the heights used
    /// longest ago are let go (and read from disk again if needed). One cache is shared by every terrain file a world
    /// opens, so the budget covers the whole world. Thread-safe.
    /// </summary>
    public sealed class HeightCache
    {
        /// <summary>64 MB: about 3,900 chunks of 65 × 65 samples, far more than the land around a camera.</summary>
        public const long DefaultBudgetBytes = 64L * 1024 * 1024;

        readonly object _gate = new object();
        readonly Dictionary<object, LinkedListNode<Entry>> _entries = new Dictionary<object, LinkedListNode<Entry>>();
        readonly LinkedList<Entry> _recent = new LinkedList<Entry>();

        public HeightCache(long budgetBytes = DefaultBudgetBytes)
        {
            if (budgetBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(budgetBytes), budgetBytes, "The budget must be positive.");
            BudgetBytes = budgetBytes;
        }

        public long BudgetBytes { get; }

        long _usedBytes;

        /// <summary>Memory the cached heights take, in bytes.</summary>
        public long UsedBytes
        {
            get
            {
                lock (_gate)
                    return _usedBytes;
            }
        }

        public int Count
        {
            get
            {
                lock (_gate)
                    return _entries.Count;
            }
        }

        /// <summary>The heights cached for <paramref name="key"/>, or loaded with <paramref name="load"/> and cached.</summary>
        public HeightField GetOrLoad(object key, Func<HeightField> load)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (load == null)
                throw new ArgumentNullException(nameof(load));

            lock (_gate)
            {
                if (_entries.TryGetValue(key, out var node))
                {
                    _recent.Remove(node);
                    _recent.AddFirst(node);
                    return node.Value.Heights;
                }

                var heights = load();
                _entries.Add(key, _recent.AddFirst(new Entry(key, heights)));
                _usedBytes += SizeOf(heights);
                Trim();
                return heights;
            }
        }

        // Lets go of the least recently used heights until within budget, never the one just added.
        void Trim()
        {
            while (_usedBytes > BudgetBytes && _recent.Count > 1)
            {
                var oldest = _recent.Last;
                _recent.RemoveLast();
                _entries.Remove(oldest.Value.Key);
                _usedBytes -= SizeOf(oldest.Value.Heights);
            }
        }

        static long SizeOf(HeightField heights) => (long)heights.Resolution * heights.Resolution * sizeof(float);

        readonly struct Entry
        {
            public Entry(object key, HeightField heights)
            {
                Key = key;
                Heights = heights;
            }

            public object Key { get; }

            public HeightField Heights { get; }
        }
    }
}
