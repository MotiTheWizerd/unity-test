using System;
using System.Linq;

namespace Forge.Editing.Palette
{
    /// <summary>
    /// How the editor's searches match: every whitespace-separated term must be found, case-insensitively, in at least
    /// one of an entry's fields. Shared by every list the assets browser searches, so they all match alike.
    /// </summary>
    public static class SearchQuery
    {
        static readonly char[] TermSeparators = { ' ', '\t' };

        /// <summary>The terms of <paramref name="query"/>; empty for a blank or null query.</summary>
        public static string[] Terms(string query) =>
            (query ?? string.Empty).Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Whether every term is in at least one of <paramref name="fields"/> (null fields never match).</summary>
        public static bool Matches(string[] terms, params string[] fields)
        {
            if (terms == null)
                throw new ArgumentNullException(nameof(terms));
            return terms.All(term => fields.Any(field => field != null && field.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }
}
