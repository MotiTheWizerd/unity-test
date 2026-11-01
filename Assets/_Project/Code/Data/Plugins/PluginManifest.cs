using System;
using System.Collections.Generic;
using System.Linq;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// Identity and dependencies of a plugin. The plugin id doubles as the
    /// namespace of every record the plugin defines.
    /// </summary>
    public sealed class PluginManifest
    {
        public PluginManifest(string id, string version, IEnumerable<string> masters = null)
        {
            var masterList = masters?.ToArray() ?? Array.Empty<string>();
            var errors = new List<string>();

            if (!Identifiers.IsValidNamespace(id))
                errors.Add($"Invalid plugin id '{id}'.");
            if (string.IsNullOrWhiteSpace(version))
                errors.Add($"Plugin '{id}' has no version.");
            foreach (var master in masterList)
            {
                if (!Identifiers.IsValidNamespace(master))
                    errors.Add($"Plugin '{id}' lists invalid master id '{master}'.");
                else if (master == id)
                    errors.Add($"Plugin '{id}' lists itself as a master.");
            }
            foreach (var duplicate in masterList.GroupBy(m => m).Where(g => g.Count() > 1))
                errors.Add($"Plugin '{id}' lists master '{duplicate.Key}' more than once.");

            if (errors.Count > 0)
                throw new DataValidationException(errors);

            Id = id;
            Version = version;
            Masters = masterList;
        }

        public string Id { get; }

        public string Version { get; }

        /// <summary>Plugins this one depends on; they must load before it.</summary>
        public IReadOnlyList<string> Masters { get; }

        public bool HasMaster(string pluginId) => Masters.Contains(pluginId);
    }
}
