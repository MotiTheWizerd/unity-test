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
    /// The per-record rules live in <see cref="ResolutionRules"/>; the fold lives
    /// in <see cref="MasterLayer.Build"/>, which the editor reuses incrementally.
    /// </remarks>
    public static class LoadOrderResolver
    {
        public static ResolvedWorld Resolve(IReadOnlyList<Plugin> loadOrder) => MasterLayer.Build(loadOrder).World;

        internal static void ValidateLoadOrder(IReadOnlyList<Plugin> loadOrder)
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
