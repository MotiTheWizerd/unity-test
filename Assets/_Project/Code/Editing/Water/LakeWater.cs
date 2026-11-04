using System;
using Forge.Data.Records;

namespace Forge.Editing.Water
{
    /// <summary>A lake and where its water lies over the terrain now.</summary>
    public sealed class LakeWater
    {
        public LakeWater(LakeRecord record, LakeExtent extent)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Extent = extent ?? throw new ArgumentNullException(nameof(extent));
        }

        public LakeRecord Record { get; }

        public LakeExtent Extent { get; }
    }
}
