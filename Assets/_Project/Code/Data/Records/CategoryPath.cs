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
    }
}
