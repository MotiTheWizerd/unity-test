using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Editing.Palette
{
    /// <summary>Folds a palette's groups into one <see cref="PaletteTab"/> per top-level category.</summary>
    public static class PaletteTabs
    {
        /// <summary>
        /// The tabs of <paramref name="palette"/>, in its group order (so categories alphabetically, uncategorized
        /// last). Groups with the same first segment share a tab. An empty palette has no tabs.
        /// </summary>
        public static IReadOnlyList<PaletteTab> From(ObjectPalette palette)
        {
            if (palette == null)
                throw new ArgumentNullException(nameof(palette));

            var tabs = new List<PaletteTab>();
            var pending = new List<PaletteGroup>();
            string current = null;
            foreach (var group in palette.Groups)
            {
                var head = CategoryPath.Head(group.Category);
                if (current != null && head != current)
                {
                    tabs.Add(new PaletteTab(current, pending.ToArray()));
                    pending.Clear();
                }

                current = head;
                pending.Add(group);
            }

            if (current != null)
                tabs.Add(new PaletteTab(current, pending.ToArray()));
            return tabs;
        }
    }
}
