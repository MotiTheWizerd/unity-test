using System.Collections.Generic;

namespace Forge.Data.Plugins
{
    /// <summary>The outcome of resolving a load order: the data plus diagnostics.</summary>
    public sealed class ResolvedWorld
    {
        internal ResolvedWorld(
            IReadOnlyList<string> loadOrder,
            IRecordStore records,
            IReadOnlyList<RecordConflict> conflicts,
            IReadOnlyList<string> warnings)
        {
            LoadOrder = loadOrder;
            Records = records;
            Conflicts = conflicts;
            Warnings = warnings;
        }

        public IReadOnlyList<string> LoadOrder { get; }

        public IRecordStore Records { get; }

        /// <summary>Overrides that silently discard another plugin's change.</summary>
        public IReadOnlyList<RecordConflict> Conflicts { get; }

        /// <summary>Suspicious but legal data, e.g. overriding a record no master defines.</summary>
        public IReadOnlyList<string> Warnings { get; }
    }
}
