using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// One way a thing can be equipped: the slots it takes together. A one-handed sword has two fits, the right hand and
    /// the left; a two-handed axe has one, both hands at once.
    /// </summary>
    public sealed class EquipFit
    {
        public EquipFit(IEnumerable<RecordId> slots)
        {
            var list = slots?.ToArray() ?? throw new ArgumentNullException(nameof(slots));
            if (list.Length == 0)
                throw new ArgumentException("A fit takes at least one slot.", nameof(slots));
            if (list.Any(slot => slot.IsDefault))
                throw new ArgumentException("A fit names its slots.", nameof(slots));
            if (list.Distinct().Count() != list.Length)
                throw new ArgumentException("A fit takes each slot once.", nameof(slots));
            Slots = list;
        }

        /// <summary>The slots it takes, in the order written.</summary>
        public IReadOnlyList<RecordId> Slots { get; }

        /// <summary>Whether it takes <paramref name="slot"/>.</summary>
        public bool Takes(RecordId slot)
        {
            foreach (var taken in Slots)
            {
                if (taken == slot)
                    return true;
            }

            return false;
        }

        /// <summary>Whether it takes exactly the slots <paramref name="other"/> does, in any order.</summary>
        public bool SameAs(EquipFit other) => other != null && Slots.Count == other.Slots.Count && Slots.All(other.Takes);

        public static EquipFit Of(params RecordId[] slots) => new EquipFit(slots);
    }
}
