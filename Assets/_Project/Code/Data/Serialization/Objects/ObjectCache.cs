using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// The decoded objects of stored blocks, kept up to a memory budget (<see cref="BudgetCache{T}"/>). One cache is
    /// shared by every objects file a world opens, so the budget covers the whole world.
    /// </summary>
    public sealed class ObjectCache : BudgetCache<IReadOnlyList<PlacedObjectRecord>>
    {
        /// <summary>64 MB: about 130,000 objects, far more than stand around a camera.</summary>
        public const long DefaultBudgetBytes = 64L * 1024 * 1024;

        /// <summary>What a placed object record costs in memory, near enough: its ids' strings and its transform.</summary>
        public const int BytesPerObject = 512;

        public ObjectCache(long budgetBytes = DefaultBudgetBytes)
            : base(budgetBytes)
        {
        }

        protected override long SizeOf(IReadOnlyList<PlacedObjectRecord> objects) => (long)objects.Count * BytesPerObject;
    }
}
