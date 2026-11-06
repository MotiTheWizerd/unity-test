using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;

namespace Forge.Game.Attributes
{
    /// <summary>
    /// The attributes actors have, as the load order resolved them, checked once and shared by every actor
    /// (<see cref="ActorAttributes"/> holds only an actor's own numbers). Each attribute gets a slot; it knows which
    /// attributes read it through their terms, and an order in which every attribute comes after the ones it reads, so a
    /// change ripples through in one pass. Every attribute named must exist, and terms must not go round in a circle.
    /// </summary>
    public sealed class AttributeSet
    {
        readonly Dictionary<RecordId, int> _slots;
        readonly int[][] _readers;
        readonly int[][] _terms;

        AttributeSet(AttributeRecord[] records, Dictionary<RecordId, int> slots, int[][] readers, int[][] terms, int[] order)
        {
            Records = records;
            _slots = slots;
            _readers = readers;
            _terms = terms;
            Order = order;
        }

        /// <summary>Every attribute, by slot.</summary>
        public IReadOnlyList<AttributeRecord> Records { get; }

        public int Count => Records.Count;

        /// <summary>Slots ordered so that each comes after every attribute its terms read.</summary>
        internal IReadOnlyList<int> Order { get; }

        /// <summary>The set of <paramref name="attributes"/>; throws <see cref="DataValidationException"/> with every problem found.</summary>
        public static AttributeSet From(IEnumerable<AttributeRecord> attributes)
        {
            if (attributes == null)
                throw new ArgumentNullException(nameof(attributes));

            var records = attributes.OrderBy(a => a.Id.ToString(), StringComparer.Ordinal).ToArray();
            var slots = new Dictionary<RecordId, int>();
            var errors = new List<string>();
            for (var i = 0; i < records.Length; i++)
            {
                if (slots.ContainsKey(records[i].Id))
                    errors.Add($"Attribute {records[i].Id} is defined twice.");
                else
                    slots.Add(records[i].Id, i);
            }

            foreach (var record in records)
            {
                foreach (var read in record.Reads().Where(read => !slots.ContainsKey(read)))
                    errors.Add($"Attribute {record.Id} reads {read}, which is no attribute.");
                if (record.RegenRate.HasValue && slots.TryGetValue(record.RegenRate.Value, out var rate) && records[rate].Kind != AttributeKind.Stat)
                    errors.Add($"Attribute {record.Id} refills by {record.RegenRate}, which is no stat.");
                if (record.RegenDelay.HasValue && slots.TryGetValue(record.RegenDelay.Value, out var delay) && records[delay].Kind != AttributeKind.Stat)
                    errors.Add($"Attribute {record.Id} waits by {record.RegenDelay}, which is no stat.");
            }

            if (errors.Count > 0)
                throw new DataValidationException(errors);

            var terms = records.Select(r => (r.Terms ?? Array.Empty<AttributeTerm>()).Select(t => slots[t.Attribute]).ToArray()).ToArray();
            var readerLists = Enumerable.Range(0, records.Length).Select(_ => new List<int>()).ToArray();
            for (var reader = 0; reader < records.Length; reader++)
            {
                foreach (var read in terms[reader].Distinct())
                    readerLists[read].Add(reader);
            }

            var readers = readerLists.Select(list => list.ToArray()).ToArray();
            var order = Ordered(records, terms);
            return new AttributeSet(records, slots, readers, terms, order);
        }

        public bool Contains(RecordId attribute) => _slots.ContainsKey(attribute);

        /// <summary>The slot of <paramref name="attribute"/>; throws when the set has no such attribute.</summary>
        public int SlotOf(RecordId attribute) =>
            _slots.TryGetValue(attribute, out var slot) ? slot : throw new KeyNotFoundException($"No attribute {attribute}.");

        /// <summary>The slots whose terms read <paramref name="slot"/>.</summary>
        internal IReadOnlyList<int> ReadersOf(int slot) => _readers[slot];

        /// <summary>The slots <paramref name="slot"/>'s terms read, in the record's order.</summary>
        internal IReadOnlyList<int> TermsOf(int slot) => _terms[slot];

        // Depth-first, each attribute after the ones it reads; a term that leads back to where it started is a cycle.
        static int[] Ordered(AttributeRecord[] records, int[][] terms)
        {
            var order = new List<int>(records.Length);
            var state = new byte[records.Length]; // 0 unvisited, 1 on the path, 2 done
            var path = new Stack<int>();

            void Visit(int slot)
            {
                if (state[slot] == 2)
                    return;
                if (state[slot] == 1)
                {
                    var loop = path.Reverse().SkipWhile(s => s != slot).Append(slot).Select(s => records[s].Id.ToString());
                    throw new DataValidationException(new[] { $"Attribute terms go round in a circle: {string.Join(" → ", loop)}." });
                }

                state[slot] = 1;
                path.Push(slot);
                foreach (var read in terms[slot])
                    Visit(read);
                path.Pop();
                state[slot] = 2;
                order.Add(slot);
            }

            for (var slot = 0; slot < records.Length; slot++)
                Visit(slot);
            return order.ToArray();
        }
    }
}
