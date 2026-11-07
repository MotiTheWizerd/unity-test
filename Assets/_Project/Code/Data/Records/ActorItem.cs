using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// Something an actor starts out carrying (<see cref="ActorRecord.Items"/>): how many of an item, and which equip slots
    /// it starts in (<see cref="Equip"/>), a copy each, so two swords may start one in each hand. Equipped is not drawn: an
    /// actor starts with its weapons sheathed.
    /// </summary>
    public sealed class ActorItem
    {
        /// <param name="item">The item (a base object with an <see cref="ItemPart"/>).</param>
        /// <param name="count">How many it carries; at least one.</param>
        /// <param name="equip">The slots copies start equipped in, one copy each; null or empty for none.</param>
        public ActorItem(RecordId item, int count = 1, IEnumerable<RecordId> equip = null)
        {
            if (item.IsDefault)
                throw new ArgumentException("An actor's item names the item.", nameof(item));
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), count, "An actor carries at least one of an item it lists.");
            var slots = equip?.ToArray();
            if (slots != null && slots.Any(slot => slot.IsDefault))
                throw new ArgumentException("An equip slot is named.", nameof(equip));
            if (slots != null && slots.Distinct().Count() != slots.Length)
                throw new ArgumentException("Each slot is named once.", nameof(equip));
            if (slots != null && slots.Length > count)
                throw new ArgumentException($"{slots.Length} slots to equip, but only {count} carried.", nameof(equip));

            Item = item;
            Count = count;
            Equip = slots != null && slots.Length > 0 ? slots : null;
        }

        public RecordId Item { get; }

        public int Count { get; }

        /// <summary>The slots copies start equipped in, one copy each; null when none (and left out of the file).</summary>
        public IReadOnlyList<RecordId> Equip { get; }
    }
}
