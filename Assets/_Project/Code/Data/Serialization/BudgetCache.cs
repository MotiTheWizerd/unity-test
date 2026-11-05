using System;
using System.Collections.Generic;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Values loaded from disk, kept up to a memory budget: when it is exceeded, the values used longest ago are let
    /// go (and loaded again if needed). One cache per kind of value is shared by every file a world opens, so the
    /// budget covers the whole world. Thread-safe.
    /// </summary>
    public abstract class BudgetCache<T> where T : class
    {
        readonly object _gate = new object();
        readonly Dictionary<object, LinkedListNode<Entry>> _entries = new Dictionary<object, LinkedListNode<Entry>>();
        readonly LinkedList<Entry> _recent = new LinkedList<Entry>();
        long _usedBytes;

        protected BudgetCache(long budgetBytes)
        {
            if (budgetBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(budgetBytes), budgetBytes, "The budget must be positive.");
            BudgetBytes = budgetBytes;
        }

        public long BudgetBytes { get; }

        /// <summary>Memory the cached values take, in bytes, as <see cref="SizeOf"/> reckons it.</summary>
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

        /// <summary>The value cached for <paramref name="key"/>, or loaded with <paramref name="load"/> and cached.</summary>
        public T GetOrLoad(object key, Func<T> load)
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
                    return node.Value.Value;
                }

                var value = load();
                _entries.Add(key, _recent.AddFirst(new Entry(key, value)));
                _usedBytes += SizeOf(value);
                Trim();
                return value;
            }
        }

        /// <summary>How much memory <paramref name="value"/> takes, for the budget.</summary>
        protected abstract long SizeOf(T value);

        // Lets go of the least recently used values until within budget, never the one just added.
        void Trim()
        {
            while (_usedBytes > BudgetBytes && _recent.Count > 1)
            {
                var oldest = _recent.Last;
                _recent.RemoveLast();
                _entries.Remove(oldest.Value.Key);
                _usedBytes -= SizeOf(oldest.Value.Value);
            }
        }

        readonly struct Entry
        {
            public Entry(object key, T value)
            {
                Key = key;
                Value = value;
            }

            public object Key { get; }

            public T Value { get; }
        }
    }
}
