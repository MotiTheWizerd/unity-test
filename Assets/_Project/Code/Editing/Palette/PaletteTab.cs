using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Editing.Palette
{
    /// <summary>
    /// One tab of the assets browser: a top-level category ("Nature") with the palette's groups under it
    /// ("Nature/Trees", "Nature/Rocks"), in display order. The empty category is the uncategorized tab.
    /// </summary>
    public sealed class PaletteTab
    {
        public PaletteTab(string category, IReadOnlyList<PaletteGroup> groups)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Groups = groups ?? throw new ArgumentNullException(nameof(groups));
            if (groups.Count == 0)
                throw new ArgumentException("A tab needs at least one group.", nameof(groups));
            if (groups.Any(g => CategoryPath.Head(g.Category) != category))
                throw new ArgumentException($"Every group of the '{category}' tab must sit under it.", nameof(groups));
            Count = groups.Sum(g => g.Objects.Count);
        }

        /// <summary>The top-level category, the first segment of every group's path; empty for uncategorized.</summary>
        public string Category { get; }

        /// <summary>The palette's groups under this category, in display order.</summary>
        public IReadOnlyList<PaletteGroup> Groups { get; }

        /// <summary>How many objects the tab holds.</summary>
        public int Count { get; }
    }
}
