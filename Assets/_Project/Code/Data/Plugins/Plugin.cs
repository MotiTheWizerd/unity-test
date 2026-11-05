using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data.Records;

namespace Forge.Data.Plugins
{
    /// <summary>
    /// An immutable unit of content: a manifest plus the records it adds or
    /// overrides and the records it deletes. Its placed objects are kept apart, a block per chunk
    /// (<see cref="Objects"/>), so a loaded plugin's objects stay on disk until their chunk is needed.
    /// </summary>
    /// <remarks>
    /// Ownership rules, checked on construction (for stored objects, when a block is read):
    /// a plugin may define records in its own namespace, and may override or
    /// delete records only in the namespaces of its declared masters.
    /// </remarks>
    public sealed class Plugin
    {
        /// <summary>A plugin whose records are all in memory; placed objects among them go into <see cref="Objects"/>.</summary>
        public Plugin(PluginManifest manifest, IEnumerable<Record> records, IEnumerable<RecordId> deletions = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));

            var all = (records ?? Enumerable.Empty<Record>()).ToArray();
            if (all.Any(r => r == null))
                throw new DataValidationException(new[] { $"Plugin '{manifest.Id}' contains a null record." });

            var recordList = all.Where(r => !(r is PlacedObjectRecord)).OrderBy(r => r.Id).ToArray();
            var deletionList = (deletions ?? Enumerable.Empty<RecordId>()).OrderBy(d => d).ToArray();
            var objects = all.OfType<PlacedObjectRecord>().ToArray();

            var errors = Validate(manifest, recordList.Select(r => r.Id).Concat(objects.Select(o => o.Id)).ToArray(), deletionList);
            if (errors.Count > 0)
                throw new DataValidationException(errors);

            Records = recordList;
            Deletions = deletionList;
            Objects = new InMemoryObjectBlocks(manifest.Id, objects);
        }

        /// <summary>A loaded plugin: its records in memory and its placed objects in <paramref name="objects"/>, read a block at a time.</summary>
        public Plugin(PluginManifest manifest, IEnumerable<Record> records, IEnumerable<RecordId> deletions, IObjectBlocks objects)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Objects = objects ?? throw new ArgumentNullException(nameof(objects));

            var recordList = (records ?? Enumerable.Empty<Record>()).OrderBy(r => r?.Id).ToArray();
            var deletionList = (deletions ?? Enumerable.Empty<RecordId>()).OrderBy(d => d).ToArray();

            var errors = new List<string>();
            if (recordList.Any(r => r == null))
                errors.Add($"Plugin '{manifest.Id}' contains a null record.");
            if (recordList.Any(r => r is PlacedObjectRecord))
                errors.Add($"Plugin '{manifest.Id}' lists placed objects among its records; they belong in its object blocks.");
            errors.AddRange(Validate(manifest, recordList.Where(r => r != null).Select(r => r.Id).ToArray(), deletionList));
            foreach (var id in objects.Overrides.Where(id => !manifest.HasMaster(id.Namespace)))
                errors.Add($"Plugin '{manifest.Id}' writes record '{id}' but '{id.Namespace}' is neither the plugin itself nor one of its masters.");
            if (errors.Count > 0)
                throw new DataValidationException(errors);

            Records = recordList;
            Deletions = deletionList;
        }

        public PluginManifest Manifest { get; }

        public string Id => Manifest.Id;

        /// <summary>Records added or overridden by this plugin, sorted by id, except placed objects (see <see cref="Objects"/>).</summary>
        public IReadOnlyList<Record> Records { get; }

        /// <summary>The plugin's placed objects, a block per chunk.</summary>
        public IObjectBlocks Objects { get; }

        /// <summary>Ids of master records this plugin removes, sorted.</summary>
        public IReadOnlyList<RecordId> Deletions { get; }

        static List<string> Validate(PluginManifest manifest, RecordId[] recordIds, RecordId[] deletions)
        {
            var errors = new List<string>();
            var id = manifest.Id;

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
