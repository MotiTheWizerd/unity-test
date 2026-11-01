using System.Collections.Generic;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// The per-record rules of load-order resolution (warnings and conflicts).
    /// Full and layered resolution both apply them, so the two can't drift apart.
    /// </summary>
    static class ResolutionRules
    {
        /// <param name="exists">Whether an earlier plugin left the record in the world.</param>
        /// <param name="previousToucher">The last earlier plugin to write or delete the id, or null.</param>
        public static void Write(Plugin plugin, RecordId id, bool exists, string previousToucher, List<RecordConflict> conflicts, List<string> warnings)
        {
            if (id.Namespace != plugin.Id && !exists)
                warnings.Add($"'{plugin.Id}' overrides '{id}', which no earlier plugin defines.");
            CheckConflict(plugin, id, previousToucher, conflicts);
        }

        public static void Delete(Plugin plugin, RecordId id, bool exists, string previousToucher, List<RecordConflict> conflicts, List<string> warnings)
        {
            if (!exists)
                warnings.Add($"'{plugin.Id}' deletes '{id}', which does not exist at that point.");
            CheckConflict(plugin, id, previousToucher, conflicts);
        }

        static void CheckConflict(Plugin plugin, RecordId id, string previousToucher, List<RecordConflict> conflicts)
        {
            if (previousToucher != null && !plugin.Manifest.HasMaster(previousToucher))
                conflicts.Add(new RecordConflict(id, previousToucher, plugin.Id));
        }
    }
}
