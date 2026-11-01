using System;

namespace Forge.Data.Records
{
    /// <summary>
    /// Base of all world data. Records are immutable: editing produces a new
    /// instance that overrides the old one in the active plugin.
    /// </summary>
    public abstract class Record
    {
        protected Record(RecordId id)
        {
            if (id.IsDefault)
                throw new ArgumentException("Record id must be set.", nameof(id));
            Id = id;
        }

        public RecordId Id { get; }

        public override string ToString() => $"{GetType().Name}({Id})";
    }
}
