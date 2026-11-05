using System;
using System.Linq;

namespace Forge.Data.Records
{
    /// <summary>
    /// Human-readable category paths for organizing content, e.g. "Nature/Trees".
    /// Segments are separated by '/'; the empty path means uncategorized.
    /// </summary>
    public static class CategoryPath
    {
        public const char Separator = '/';

        /// <summary>
        /// Trims every segment and rejoins them, so " Nature / Trees " becomes "Nature/Trees".
        /// Null or blank becomes the empty path. Throws on an empty segment ("Nature//Trees").
        /// </summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var segments = path.Split(Separator).Select(s => s.Trim()).ToArray();
            if (segments.Any(s => s.Length == 0))
                throw new ArgumentException($"Category '{path}' has an empty segment.", nameof(path));

            return string.Join(Separator.ToString(), segments);
        }

        /// <summary>The first segment: "Nature" of "Nature/Trees". Empty for the empty path.</summary>
        public static string Head(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            var cut = path.IndexOf(Separator);
            return cut < 0 ? path : path.Substring(0, cut);
        }

        /// <summary>Everything after the first segment: "Trees" of "Nature/Trees". Empty when there is none.</summary>
        public static string Tail(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            var cut = path.IndexOf(Separator);
            return cut < 0 ? string.Empty : path.Substring(cut + 1);
        }
    }
}
