using System;

namespace Forge.Data
{
    /// <summary>
    /// Stable, load-order-independent identity of a record, written as
    /// <c>namespace:name</c> (e.g. <c>forge.core:trees/oak_01</c>).
    /// The namespace is the id of the plugin that defines the record.
    /// </summary>
    public readonly struct RecordId : IEquatable<RecordId>, IComparable<RecordId>
    {
        public const char Separator = ':';

        public RecordId(string ns, string name)
        {
            if (!Identifiers.IsValidNamespace(ns))
                throw new ArgumentException($"Invalid record namespace '{ns}'.", nameof(ns));
            if (!Identifiers.IsValidName(name))
                throw new ArgumentException($"Invalid record name '{name}'.", nameof(name));

            Namespace = ns;
            Name = name;
        }

        public string Namespace { get; }

        public string Name { get; }

        /// <summary>True for <c>default(RecordId)</c>, which identifies nothing.</summary>
        public bool IsDefault => Namespace == null;

        public static RecordId Parse(string value)
        {
            if (TryParse(value, out var id))
                return id;
            throw new FormatException($"'{value}' is not a valid record id. Expected 'namespace:name'.");
        }

        public static bool TryParse(string value, out RecordId id)
        {
            id = default;
            if (value == null)
                return false;

            var separatorIndex = value.IndexOf(Separator);
            if (separatorIndex < 0 || separatorIndex != value.LastIndexOf(Separator))
                return false;

            var ns = value.Substring(0, separatorIndex);
            var name = value.Substring(separatorIndex + 1);
            if (!Identifiers.IsValidNamespace(ns) || !Identifiers.IsValidName(name))
                return false;

            id = new RecordId(ns, name);
            return true;
        }

        public bool Equals(RecordId other) =>
            string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) &&
            string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is RecordId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Namespace == null ? 0 : StringComparer.Ordinal.GetHashCode(Namespace);
                return (hash * 397) ^ (Name == null ? 0 : StringComparer.Ordinal.GetHashCode(Name));
            }
        }

        public int CompareTo(RecordId other)
        {
            var byNamespace = string.CompareOrdinal(Namespace, other.Namespace);
            return byNamespace != 0 ? byNamespace : string.CompareOrdinal(Name, other.Name);
        }

        public override string ToString() => IsDefault ? string.Empty : Namespace + Separator + Name;

        public static bool operator ==(RecordId left, RecordId right) => left.Equals(right);

        public static bool operator !=(RecordId left, RecordId right) => !left.Equals(right);
    }
}
