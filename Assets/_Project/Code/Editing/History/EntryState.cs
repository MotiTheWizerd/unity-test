using System;
using Forge.Data.Records;

namespace Forge.Editing.History
{
    public enum EntryKind
    {
        /// <summary>The active plugin says nothing about the record.</summary>
        Absent,

        /// <summary>The active plugin adds or overrides the record.</summary>
        Written,

        /// <summary>The active plugin deletes a master's record.</summary>
        Deleted,
    }

    /// <summary>What the active plugin holds for one record id. Records are immutable, so equality is by reference.</summary>
    public readonly struct EntryState : IEquatable<EntryState>
    {
        EntryState(EntryKind kind, Record record)
        {
            Kind = kind;
            Record = record;
        }

        public static EntryState Absent => default;

        public static EntryState Deleted => new EntryState(EntryKind.Deleted, null);

        public EntryKind Kind { get; }

        /// <summary>The written record; null unless <see cref="Kind"/> is <see cref="EntryKind.Written"/>.</summary>
        public Record Record { get; }

        public static EntryState Written(Record record) =>
            new EntryState(EntryKind.Written, record ?? throw new ArgumentNullException(nameof(record)));

        public bool Equals(EntryState other) => Kind == other.Kind && ReferenceEquals(Record, other.Record);

        public override bool Equals(object obj) => obj is EntryState other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ (Record?.GetHashCode() ?? 0);

        public override string ToString() => Kind == EntryKind.Written ? $"Written({Record.Id})" : Kind.ToString();
    }
}
