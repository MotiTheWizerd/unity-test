using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// An immutable unit of content: a manifest plus the records it adds or
    /// overrides and the records it deletes.
    /// </summary>
    /// <remarks>
    /// Ownership rules, checked on construction:
    /// a plugin may define records in its own namespace, and may override or
    /// delete records only in the namespaces of its declared masters.
    /// </remarks>
    public sealed class Plugin
    {
        public Plugin(PluginManifest manifest, IEnumerable<Record> records, IEnumerable<RecordId> deletions = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));

            var recordList = (records ?? Enumerable.Empty<Record>()).OrderBy(r => r?.Id).ToArray();
            var deletionList = (deletions ?? Enumerable.Empty<RecordId>()).OrderBy(d => d).ToArray();

            var errors = Validate(manifest, recordList, deletionList);
            if (errors.Count > 0)
                throw new DataValidationException(errors);

            Records = recordList;
            Deletions = deletionList;
        }

        public PluginManifest Manifest { get; }

        public string Id => Manifest.Id;

        /// <summary>Records added or overridden by this plugin, sorted by id.</summary>
        public IReadOnlyList<Record> Records { get; }

        /// <summary>Ids of master records this plugin removes, sorted.</summary>
        public IReadOnlyList<RecordId> Deletions { get; }

        static List<string> Validate(PluginManifest manifest, Record[] records, RecordId[] deletions)
        {
            var errors = new List<string>();
            var id = manifest.Id;

            if (records.Any(r => r == null))
                errors.Add($"Plugin '{id}' contains a null record.");

            var recordIds = records.Where(r => r != null).Select(r => r.Id).ToArray();

            foreach (var duplicate in recordIds.GroupBy(r => r).Where(g => g.Count() > 1))
                errors.Add($"Plugin '{id}' contains record '{duplicate.Key}' more than once.");

            foreach (var recordId in recordIds)
            {
                if (recordId.Namespace != id && !manifest.HasMaster(recordId.Namespace))
                    errors.Add($"Plugin '{id}' writes record '{recordId}' but '{recordId.Namespace}' is neither the plugin itself nor one of its masters.");
            }

            foreach (var deletion in deletions)
            {
                if (deletion.IsDefault)
                    errors.Add($"Plugin '{id}' contains an empty deletion id.");
                else if (!manifest.HasMaster(deletion.Namespace))
                    errors.Add($"Plugin '{id}' deletes record '{deletion}' but '{deletion.Namespace}' is not one of its masters.");
            }

            foreach (var both in recordIds.Intersect(deletions))
                errors.Add($"Plugin '{id}' both writes and deletes record '{both}'.");

            foreach (var duplicate in deletions.GroupBy(d => d).Where(g => g.Count() > 1))
                errors.Add($"Plugin '{id}' deletes record '{duplicate.Key}' more than once.");

            return errors;
        }
    }
}
