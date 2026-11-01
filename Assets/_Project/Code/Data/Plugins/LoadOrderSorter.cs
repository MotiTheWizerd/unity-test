using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// Orders plugins so every master loads before its dependents. Ties are broken
    /// by plugin id, so the same set of plugins always yields the same order.
    /// </summary>
    public static class LoadOrderSorter
    {
        public static IReadOnlyList<Plugin> Sort(IEnumerable<Plugin> plugins)
        {
            var all = plugins?.ToList() ?? throw new ArgumentNullException(nameof(plugins));
            var byId = new Dictionary<string, Plugin>();
            var errors = new List<string>();

            foreach (var plugin in all)
            {
                if (!byId.ContainsKey(plugin.Id))
                    byId.Add(plugin.Id, plugin);
                else
                    errors.Add($"Plugin '{plugin.Id}' was found more than once.");
            }

            foreach (var plugin in byId.Values)
            foreach (var master in plugin.Manifest.Masters.Where(m => !byId.ContainsKey(m)))
                errors.Add($"Plugin '{plugin.Id}' requires missing master '{master}'.");

            if (errors.Count > 0)
                throw new DataValidationException(errors);

            // Kahn's algorithm with an ordinal-sorted ready set.
            var pendingMasters = byId.Values.ToDictionary(p => p.Id, p => p.Manifest.Masters.Count);
            var ready = new SortedSet<string>(pendingMasters.Where(p => p.Value == 0).Select(p => p.Key), StringComparer.Ordinal);
            var order = new List<Plugin>();

            while (ready.Count > 0)
            {
                var next = ready.Min;
                ready.Remove(next);
                order.Add(byId[next]);

                foreach (var dependent in byId.Values.Where(p => p.Manifest.HasMaster(next)))
                {
                    if (--pendingMasters[dependent.Id] == 0)
                        ready.Add(dependent.Id);
                }
            }

            if (order.Count != byId.Count)
            {
                var cyclic = byId.Keys.Except(order.Select(p => p.Id)).OrderBy(id => id, StringComparer.Ordinal);
                throw new DataValidationException(new[] { $"Plugins have circular master dependencies: {string.Join(", ", cyclic)}." });
            }

            return order;
        }
    }
}
