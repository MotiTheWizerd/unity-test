using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Forge.Data.Json
{
    /// <summary>
    /// Human-readable plugin format. Output is deterministic (records and
    /// deletions sorted by id) so plugin files diff and merge cleanly.
    /// </summary>
    public sealed class JsonPluginSerializer : IPluginSerializer
    {
        public const int FormatVersion = 1;

        readonly JsonSerializer _serializer;

        public JsonPluginSerializer(RecordTypeRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters =
                {
                    new RecordConverter(registry),
                    new RecordIdConverter(),
                    new Vector3Converter(),
                    new QuaternionConverter(),
                    new ChunkCoordConverter(),
                    new HeightFieldConverter(),
                },
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error,
                // Optional fields (e.g. a base object's variation) are left out instead of written as null.
                NullValueHandling = NullValueHandling.Ignore,
                MaxDepth = 32,
            });
        }

        public string FileExtension => ".json";

        public void Write(Plugin plugin, TextWriter writer)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var document = new PluginDocument
            {
                Format = FormatVersion,
                Manifest = new ManifestDocument
                {
                    Id = plugin.Manifest.Id,
                    Version = plugin.Manifest.Version,
                    Masters = new List<string>(plugin.Manifest.Masters),
                },
                Records = new List<Record>(plugin.Records),
                Deletions = new List<RecordId>(plugin.Deletions),
            };

            _serializer.Serialize(writer, document);
        }

        public Plugin Read(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            PluginDocument document;
            try
            {
                document = (PluginDocument)_serializer.Deserialize(reader, typeof(PluginDocument));
            }
            catch (Exception ex) when (
                ex is JsonException || ex is ArgumentException || ex is FormatException ||
                ex is InvalidCastException || ex is TargetInvocationException)
            {
                throw new DataValidationException(new[] { $"Malformed plugin file: {Unwrap(ex).Message}" }, ex);
            }

            if (document == null)
                throw new DataValidationException(new[] { "Plugin file is empty." });
            if (document.Format != FormatVersion)
                throw new DataValidationException(new[] { $"Unsupported plugin format {document.Format}; expected {FormatVersion}." });
            if (document.Manifest == null)
                throw new DataValidationException(new[] { "Plugin file has no manifest." });

            var manifest = new PluginManifest(document.Manifest.Id, document.Manifest.Version, document.Manifest.Masters);
            return new Plugin(manifest, document.Records, document.Deletions);
        }

        // Record constructors validate their arguments; Newtonsoft wraps those
        // failures, so surface the innermost message to the modder.
        static Exception Unwrap(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }

        // File-format shape. Kept separate from the domain types so the format
        // can evolve (FormatVersion) without touching the data model.
        sealed class PluginDocument
        {
            public int Format { get; set; }

            public ManifestDocument Manifest { get; set; }

            public List<Record> Records { get; set; }

            public List<RecordId> Deletions { get; set; }
        }

        sealed class ManifestDocument
        {
            public string Id { get; set; }

            public string Version { get; set; }

            public List<string> Masters { get; set; }
        }
    }
}
