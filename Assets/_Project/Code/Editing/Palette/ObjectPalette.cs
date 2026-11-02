using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Data.Records;

namespace Forge.Editing.Palette
{
    /// <summary>
    /// The base objects the editor can place, grouped by category and searchable.
    /// Immutable: <see cref="Filter"/> returns a narrower palette in the same order.
    /// </summary>
    /// <remarks>
    /// Display order: categories alphabetically (uncategorized last), then display name, then id.
    /// <see cref="Objects"/> follows that order, so numbered shortcuts match what the panel shows.
    /// </remarks>
    public sealed class ObjectPalette
    {
        static readonly StringComparer NameOrder = StringComparer.OrdinalIgnoreCase;
        static readonly char[] TermSeparators = { ' ', '\t' };

        readonly Dictionary<RecordId, int> _indexById;

        ObjectPalette(IReadOnlyList<BaseObjectRecord> sorted)
        {
            Objects = sorted;
            _indexById = new Dictionary<RecordId, int>(sorted.Count);
            for (var i = 0; i < sorted.Count; i++)
                _indexById.Add(sorted[i].Id, i);

            Groups = sorted
                .GroupBy(r => r.Category)
                .Select(g => new PaletteGroup(g.Key, g.ToArray()))
                .ToArray();
        }

        public static ObjectPalette Empty { get; } = new ObjectPalette(Array.Empty<BaseObjectRecord>());

        public static ObjectPalette From(IEnumerable<BaseObjectRecord> objects)
        {
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));

            var sorted = objects
                .OrderBy(r => r.Category.Length == 0) // uncategorized last
                .ThenBy(r => r.Category, NameOrder)
                .ThenBy(r => r.DisplayName, NameOrder)
                .ThenBy(r => r.Id)
                .ToArray();
            return new ObjectPalette(sorted);
        }

        /// <summary>Every object in display order.</summary>
        public IReadOnlyList<BaseObjectRecord> Objects { get; }

        /// <summary>The objects by category, in display order. Empty categories are never listed.</summary>
        public IReadOnlyList<PaletteGroup> Groups { get; }

        public int Count => Objects.Count;

        /// <summary>Position of <paramref name="id"/> in <see cref="Objects"/>, or -1.</summary>
        public int IndexOf(RecordId id) => _indexById.TryGetValue(id, out var index) ? index : -1;

        public bool Contains(RecordId id) => _indexById.ContainsKey(id);

        /// <summary>
        /// Objects matching every whitespace-separated term of <paramref name="query"/>, case-insensitively,
        /// in their display name, category or id. A blank query returns this palette.
        /// </summary>
        public ObjectPalette Filter(string query)
        {
            var terms = (query ?? string.Empty).Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
                return this;

            return new ObjectPalette(Objects.Where(r => terms.All(term => Matches(r, term))).ToArray());
        }

        static bool Matches(BaseObjectRecord record, string term) =>
            Contains(record.DisplayName, term) || Contains(record.Category, term) || Contains(record.Id.ToString(), term);

        static bool Contains(string text, string term) =>
            text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
