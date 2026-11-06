using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing.Palette;

namespace Forge.Editing
{
    /// <summary>The materials a world offers for dressing placed objects' slots, for pickers.</summary>
    public static class Materials
    {
        /// <summary>Every material of the world, by display name (then id, so the order is stable).</summary>
        public static IReadOnlyList<MaterialRecord> Of(IRecordStore store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            return store.OfType<MaterialRecord>()
                .OrderBy(material => material.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(material => material.Id)
                .ToList();
        }

        /// <summary>
        /// The materials matching every term of <paramref name="query"/> in their display name or id, in their order
        /// (<see cref="SearchQuery"/>, as the object palette searches). A blank query returns them all.
        /// </summary>
        public static IReadOnlyList<MaterialRecord> Filter(IReadOnlyList<MaterialRecord> materials, string query)
        {
            if (materials == null)
                throw new ArgumentNullException(nameof(materials));

            var terms = SearchQuery.Terms(query);
            return terms.Length == 0
                ? materials
                : materials.Where(material => SearchQuery.Matches(terms, material.DisplayName, material.Id.ToString())).ToList();
        }

        /// <summary>
        /// A slot name as people read it: words split where a lower-case letter or digit meets a capital, underscores
        /// and dashes as spaces ("WoodDark" → "Wood Dark", "roof_tiles" → "roof tiles").
        /// </summary>
        public static string SlotLabel(string slot)
        {
            if (string.IsNullOrEmpty(slot))
                return string.Empty;

            var label = new System.Text.StringBuilder(slot.Length + 4);
            for (var i = 0; i < slot.Length; i++)
            {
                var c = slot[i];
                if (c == '_' || c == '-')
                    c = ' ';
                else if (i > 0 && char.IsUpper(c) && (char.IsLower(slot[i - 1]) || char.IsDigit(slot[i - 1])))
                    label.Append(' ');
                label.Append(c);
            }

            return label.ToString().Trim();
        }
    }
}
