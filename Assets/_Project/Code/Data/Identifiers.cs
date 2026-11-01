using System.Text.RegularExpressions;

namespace Forge.Data
{
    /// <summary>
    /// Single source of truth for identifier syntax used across the data model.
    /// All identifiers are lowercase ASCII so they are unambiguous on every
    /// file system and in every diff.
    /// </summary>
    public static class Identifiers
    {
        // Plugin ids and record namespaces: dot-separated segments, e.g. "forge.core".
        static readonly Regex NamespacePattern =
            new Regex(@"^[a-z0-9_]+(\.[a-z0-9_]+)*$", RegexOptions.Compiled);

        // Record names: segments separated by '.', '/' or '-', e.g. "trees/oak_01".
        static readonly Regex NamePattern =
            new Regex(@"^[a-z0-9_]+([./-][a-z0-9_]+)*$", RegexOptions.Compiled);

        // Short schema keys, e.g. record type discriminators: "placed_object".
        static readonly Regex KeyPattern =
            new Regex(@"^[a-z0-9_]+$", RegexOptions.Compiled);

        public static bool IsValidNamespace(string value) => value != null && NamespacePattern.IsMatch(value);

        public static bool IsValidName(string value) => value != null && NamePattern.IsMatch(value);

        public static bool IsValidKey(string value) => value != null && KeyPattern.IsMatch(value);
    }
}
