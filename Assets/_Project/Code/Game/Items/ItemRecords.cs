using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Game.Items
{
    /// <summary>Reading items out of the world's records: a base object with an <see cref="ItemPart"/>.</summary>
    public static class ItemRecords
    {
        /// <summary>
        /// The item <paramref name="id"/> names; false when the world has no such base object or it is no item (its
        /// plugin unloaded, or a mod took the part away), which an inventory then neither lists nor weighs.
        /// </summary>
        public static bool TryGet(IRecordStore records, RecordId id, out BaseObjectRecord item)
        {
            item = records != null && records.TryGet(id, out var record) && record is BaseObjectRecord baseObject && baseObject.Item != null
                ? baseObject
                : null;
            return item != null;
        }
    }
}
