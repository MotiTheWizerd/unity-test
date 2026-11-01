using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// Resolves an ordered list of plugins into a single world.
    /// </summary>
    /// <remarks>
    /// v1 rule: the last plugin to touch a record wins the whole record (no
    /// field-level merge). An override is a <see cref="RecordConflict"/> when the
    /// overriding plugin does not list the previous winner as a direct master.
    /// </remarks>
    public static class LoadOrderResolver
    {
        public static ResolvedWorld Resolve(IReadOnlyList<Plugin> loadOrder)
        {
            ValidateLoadOrder(loadOrder);

            var entries = new Dictionary<RecordId, RecordStore.Entry>();
            var lastToucher = new Dictionary<RecordId, string>();
            var conflicts = new List<RecordConflict>();
            var warnings = new List<string>();

            foreach (var plugin in loadOrder)
            {
                foreach (var record in plugin.Records)
                {
                    var isOverride = record.Id.Namespace != plugin.Id;
                    if (isOverride && !entries.ContainsKey(record.Id))
                        warnings.Add($"'{plugin.Id}' overrides '{record.Id}', which no earlier plugin defines.");

                    Touch(plugin, record.Id, lastToucher, conflicts);
                    entries[record.Id] = new RecordStore.Entry(record, plugin.Id);
                }

                foreach (var deletion in plugin.Deletions)
                {
                    if (!entries.ContainsKey(deletion))
                        warnings.Add($"'{plugin.Id}' deletes '{deletion}', which does not exist at that point.");

                    Touch(plugin, deletion, lastToucher, conflicts);
                    entries.Remove(deletion);
                }
            }

            return new ResolvedWorld(
                loadOrder.Select(p => p.Id).ToArray(),
                new RecordStore(entries),
                conflicts,
                warnings);
        }

        static void Touch(Plugin plugin, RecordId id, Dictionary<RecordId, string> lastToucher, List<RecordConflict> conflicts)
        {
            if (lastToucher.TryGetValue(id, out var previous) && !plugin.Manifest.HasMaster(previous))
                conflicts.Add(new RecordConflict(id, previous, plugin.Id));
            lastToucher[id] = plugin.Id;
        }

        static void ValidateLoadOrder(IReadOnlyList<Plugin> loadOrder)
        {
            if (loadOrder == null || loadOrder.Any(p => p == null))
                throw new DataValidationException(new[] { "Load order must not be null or contain null plugins." });

            var errors = new List<string>();
            var loaded = new HashSet<string>();

            foreach (var plugin in loadOrder)
            {
                if (!loaded.Add(plugin.Id))
                {
                    errors.Add($"Plugin '{plugin.Id}' appears more than once in the load order.");
                    continue;
                }

                foreach (var master in plugin.Manifest.Masters.Where(m => !loaded.Contains(m)))
                {
                    errors.Add(loadOrder.Any(p => p.Id == master)
                        ? $"Plugin '{plugin.Id}' must load after its master '{master}'."
                        : $"Plugin '{plugin.Id}' requires missing master '{master}'.");
                }
            }

            if (errors.Count > 0)
                throw new DataValidationException(errors);
        }
    }
}
