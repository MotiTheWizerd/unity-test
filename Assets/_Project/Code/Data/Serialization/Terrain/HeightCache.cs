using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// The decoded heights of stored chunks, kept up to a memory budget (<see cref="BudgetCache{T}"/>). One cache is
    /// shared by every terrain file a world opens, so the budget covers the whole world.
    /// </summary>
    public sealed class HeightCache : BudgetCache<HeightField>
    {
        /// <summary>64 MB: about 3,900 chunks of 65 × 65 samples, far more than the land around a camera.</summary>
        public const long DefaultBudgetBytes = 64L * 1024 * 1024;

        public HeightCache(long budgetBytes = DefaultBudgetBytes)
            : base(budgetBytes)
        {
        }

        protected override long SizeOf(HeightField heights) => (long)heights.Resolution * heights.Resolution * sizeof(float);
    }
}
