using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Editing.Inspection
{
    /// <summary>
    /// What the inspector looks at: the selected records as they stand in one version of the world, taken once when the
    /// selection or the world changes, so every inspector section reads the same snapshot instead of looking records up
    /// again each frame. Any kind of record can be inspected; each section picks the kinds it knows (<see cref="All{T}"/>).
    /// Ids that no longer name a record are left out.
    /// </summary>
    public sealed class Inspection
    {
        public static readonly Inspection Empty = new Inspection(null, Array.Empty<Record>());

        Inspection(IRecordStore world, IReadOnlyList<Record> records)
        {
            World = world;
            Records = records;
        }

        /// <summary>The world the records were read from; null for <see cref="Empty"/>.</summary>
        public IRecordStore World { get; }

        /// <summary>The selected records, in selection order.</summary>
        public IReadOnlyList<Record> Records { get; }

        public int Count => Records.Count;

        public bool IsEmpty => Records.Count == 0;

        /// <summary>The selected records of kind <typeparamref name="T"/>.</summary>
        public IReadOnlyList<T> All<T>() where T : Record => Records.OfType<T>().ToList();

        /// <summary>True when every selected record is a <typeparamref name="T"/> (and there is at least one).</summary>
        public bool AllAre<T>() where T : Record => Records.Count > 0 && Records.All(r => r is T);

        /// <summary>The records named by <paramref name="ids"/> in <paramref name="world"/>.</summary>
        public static Inspection Of(IRecordStore world, IEnumerable<RecordId> ids)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            var records = new List<Record>();
            foreach (var id in ids)
            {
                if (world.TryGet(id, out var record))
                    records.Add(record);
            }

            return new Inspection(world, records);
        }

        /// <summary>The record <paramref name="id"/> of kind <typeparamref name="T"/> in the inspected world, or null.</summary>
        public T Find<T>(RecordId id) where T : Record =>
            World != null && World.TryGet(id, out var record) ? record as T : null;

        /// <summary>
        /// What <paramref name="read"/> gives for every one of <paramref name="records"/>: the value when they all agree
        /// (within <paramref name="same"/>; equality by default), or mixed. None: not <see cref="Shared{T}.Any"/>.
        /// </summary>
        public static Shared<TValue> Shared<TRecord, TValue>(IReadOnlyList<TRecord> records, Func<TRecord, TValue> read, Func<TValue, TValue, bool> same = null)
        {
            if (records == null)
                throw new ArgumentNullException(nameof(records));
            if (read == null)
                throw new ArgumentNullException(nameof(read));
            if (records.Count == 0)
                return default;

            same = same ?? EqualityComparer<TValue>.Default.Equals;
            var first = read(records[0]);
            for (var i = 1; i < records.Count; i++)
            {
                if (!same(first, read(records[i])))
                    return new Shared<TValue>(first, true);
            }

            return new Shared<TValue>(first, false);
        }
    }

    /// <summary>A value the inspected records share, or the first of differing ones (<see cref="Mixed"/>).</summary>
    public readonly struct Shared<T>
    {
        public Shared(T value, bool mixed)
        {
            Value = value;
            Mixed = mixed;
            Any = true;
        }

        /// <summary>The shared value; the first record's when <see cref="Mixed"/>.</summary>
        public T Value { get; }

        /// <summary>True when the records differ.</summary>
        public bool Mixed { get; }

        /// <summary>False when there were no records to read.</summary>
        public bool Any { get; }
    }
}
