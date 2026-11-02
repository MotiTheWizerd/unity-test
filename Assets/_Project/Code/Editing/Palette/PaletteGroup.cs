using System;
using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.Editing.Palette
{
    /// <summary>The palette's objects in one category, in display order.</summary>
    public sealed class PaletteGroup
    {
        public PaletteGroup(string category, IReadOnlyList<BaseObjectRecord> objects)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        /// <summary>Normalized <see cref="CategoryPath"/>; empty for uncategorized objects.</summary>
        public string Category { get; }

        public IReadOnlyList<BaseObjectRecord> Objects { get; }
    }
}
