using System;
using Forge.Data.Terrain;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// The decoded blocks of stored chunks, heights and ground paint alike, kept up to a memory budget
    /// (<see cref="BudgetCache{T}"/>). One cache is shared by every terrain file a world opens, so one budget covers
    /// the whole world's land.
    /// </summary>
    public sealed class TerrainCache : BudgetCache<object>
    {
        /// <summary>64 MB: about 2,000 chunks with their heights (65 × 65 floats) and two layers of paint.</summary>
        public const long DefaultBudgetBytes = 64L * 1024 * 1024;

        public TerrainCache(long budgetBytes = DefaultBudgetBytes)
            : base(budgetBytes)
        {
        }

        internal HeightField Heights(object key, Func<HeightField> load) => (HeightField)GetOrLoad(key, load);

        internal GroundPaint Paint(object key, Func<GroundPaint> load) => (GroundPaint)GetOrLoad(key, load);

        protected override long SizeOf(object block)
        {
            switch (block)
            {
                case HeightField heights:
                    return (long)heights.Resolution * heights.Resolution * sizeof(float);
                case GroundPaint paint:
                    return (long)paint.Layers.Count * GroundPaint.TexelCount;
                default:
                    throw new ArgumentException($"A terrain cache holds heights and paint, not {block?.GetType().Name}.", nameof(block));
            }
        }
    }
}
