using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Data.Terrain;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Forge.Data.Json
{
    /// <summary>
    /// Human-readable plugin format. Output is deterministic (records and
    /// deletions sorted by id) so plugin files diff and merge cleanly. Terrain chunks carry only their resolution
    /// and a block number in the terrain file the plugin names (<c>"terrain"</c>); the heights live there. Placed
    /// objects are not in the JSON at all: they live in the objects file the plugin names (<c>"objects"</c>).
    /// </summary>
    public sealed class JsonPluginSerializer : IPluginSerializer
    {
        /// <summary>3: placed objects moved out of the JSON into an objects file (2: terrain heights into a terrain file).</summary>
        public const int FormatVersion = 3;

        const int MaxDepth = 32;

        readonly RecordTypeRegistry _registry;

        public JsonPluginSerializer(RecordTypeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string FileExtension => ".json";

        public void Write(Plugin plugin, TextWriter writer, TerrainLink terrain, ObjectsLink objects)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (terrain == null && plugin.Records.OfType<TerrainChunkRecord>().Any())
                throw new ArgumentException($"Plugin '{plugin.Id}' has terrain chunks but no terrain file to name.", nameof(terrain));
            if (objects == null && plugin.Objects.TotalCount > 0)
                throw new ArgumentException($"Plugin '{plugin.Id}' has placed objects but no objects file to name.", nameof(objects));

            var document = new PluginDocument
            {
                Format = FormatVersion,
                Manifest = new ManifestDocument
                {
                    Id = plugin.Manifest.Id,
                    Version = plugin.Manifest.Version,
                    Masters = new List<string>(plugin.Manifest.Masters),
                },
                Terrain = terrain?.FileName,
                Objects = objects?.FileName,
                Records = new List<Record>(plugin.Records),
                Deletions = new List<RecordId>(plugin.Deletions),
            };

            CreateSerializer(new ChunkHeightsConverter(terrain?.Blocks), new ChunkPaintConverter(terrain?.Blocks)).Serialize(writer, document);
        }

        public Plugin Read(TextReader reader, Func<string, ITerrainBlockReader> openTerrain, Func<string, PluginManifest, IObjectBlocks> openObjects)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            if (openTerrain == null)
                throw new ArgumentNullException(nameof(openTerrain));
            if (openObjects == null)
                throw new ArgumentNullException(nameof(openObjects));

            PluginDocument document;
            try
            {
                document = ReadDocument(new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None, MaxDepth = MaxDepth }, openTerrain, openObjects);
            }
            catch (Exception ex) when (
                ex is JsonException || ex is ArgumentException || ex is FormatException ||
                ex is InvalidCastException || ex is TargetInvocationException)
            {
                throw new DataValidationException(new[] { $"Malformed plugin file: {Unwrap(ex).Message}" }, ex);
            }

            if (document.Manifest == null)
                throw new DataValidationException(new[] { "Plugin file has no manifest." });

            var manifest = document.ManifestRead;
            // The strict constructor: objects among the records are an error, they belong in the objects file.
            return new Plugin(manifest, document.Records, document.Deletions, document.ObjectBlocks ?? InMemoryObjectBlocks.Empty(manifest.Id));
        }

        // One streaming pass over the document's fields, in the order they are written: "format" first (so an old
        // file is named as such before its records fail to read), "manifest" before "objects" (the objects file is
        // checked against it), and "terrain" before "records", so the chunks can refer into the terrain file as they
        // are read. The terrain file is opened at the first chunk; the objects file when named.
        PluginDocument ReadDocument(JsonReader json, Func<string, ITerrainBlockReader> openTerrain, Func<string, PluginManifest, IObjectBlocks> openObjects)
        {
            if (!json.Read())
                throw new DataValidationException(new[] { "Plugin file is empty." });
            if (json.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException("A plugin file is a JSON object.");

            var document = new PluginDocument();
            var terrain = new TerrainBinding(openTerrain);
            var serializer = CreateSerializer(new ChunkHeightsConverter(terrain), new ChunkPaintConverter(terrain));
            var formatSeen = false;
            while (json.Read() && json.TokenType == JsonToken.PropertyName)
            {
                var field = (string)json.Value;
                if (!json.Read())
                    throw new JsonSerializationException($"Field '{field}' has no value.");
                if (!formatSeen && field != "format")
                    throw new JsonSerializationException("A plugin file starts with its \"format\".");

                switch (field)
                {
                    case "format":
                        document.Format = serializer.Deserialize<int>(json);
                        CheckFormat(document.Format);
                        formatSeen = true;
                        break;
                    case "manifest":
                        document.Manifest = serializer.Deserialize<ManifestDocument>(json);
                        document.ManifestRead = new PluginManifest(document.Manifest.Id, document.Manifest.Version, document.Manifest.Masters);
                        break;
                    case "terrain":
                        terrain.Name = serializer.Deserialize<string>(json);
                        break;
                    case "objects":
                        document.Objects = serializer.Deserialize<string>(json);
                        if (document.ManifestRead == null)
                            throw new JsonSerializationException("A plugin file names its \"objects\" after its \"manifest\".");
                        document.ObjectBlocks = openObjects(document.Objects, document.ManifestRead);
                        break;
                    case "records":
                        document.Records = serializer.Deserialize<List<Record>>(json);
                        break;
                    case "deletions":
                        document.Deletions = serializer.Deserialize<List<RecordId>>(json);
                        break;
                    default:
                        throw new JsonSerializationException($"Unknown field '{field}' in the plugin file.");
                }
            }

            if (!formatSeen)
                throw new DataValidationException(new[] { "Plugin file has no format." });
            return document;
        }

        static void CheckFormat(int format)
        {
            if (format == FormatVersion)
                return;

            throw new DataValidationException(new[] { format == 1 || format == 2
                ? $"Plugin format {format} kept {(format == 1 ? "terrain heights and placed objects" : "placed objects")} inside the JSON; this Forge reads format {FormatVersion}, which keeps them in side files. Regenerate the plugin."
                : $"Unsupported plugin format {format}; expected {FormatVersion}." });
        }

        // A serializer per read or write: the terrain blocks differ per plugin, and converters hold no shared state.
        JsonSerializer CreateSerializer(ChunkHeightsConverter heights, ChunkPaintConverter paint) => JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters =
            {
                new RecordConverter(_registry),
                new RecordIdConverter(),
                new Vector3Converter(),
                new QuaternionConverter(),
                new ChunkCoordConverter(),
                heights,
                paint,
                // Enums by name ("worldspace"), so files stay readable and survive reordering the enum.
                new StringEnumConverter(new CamelCaseNamingStrategy(), allowIntegerValues: false),
            },
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            // Optional fields (e.g. a base object's placement rules) are left out instead of written as null.
            NullValueHandling = NullValueHandling.Ignore,
            MaxDepth = MaxDepth,
        });

        // Record constructors validate their arguments; Newtonsoft wraps those
        // failures, so surface the innermost message to the modder.
        static Exception Unwrap(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }

        // The terrain file a plugin names, opened when its first chunk is read: a plugin without terrain opens none.
        sealed class TerrainBinding : ITerrainBlockReader
        {
            readonly Func<string, ITerrainBlockReader> _open;
            ITerrainBlockReader _file;

            public TerrainBinding(Func<string, ITerrainBlockReader> open)
            {
                _open = open;
            }

            public string Name { get; set; }

            public ChunkHeights Block(int index, int resolution) => File(index).Block(index, resolution);

            public ChunkPaint Paint(int index, IReadOnlyList<RecordId> layers) => File(index).Paint(index, layers);

            ITerrainBlockReader File(int index)
            {
                if (Name == null)
                    throw new JsonSerializationException($"A terrain chunk refers to block {index}, but the plugin names no terrain file before its records.");
                return _file ?? (_file = _open(Name));
            }
        }

        // File-format shape. Kept separate from the domain types so the format
        // can evolve (FormatVersion) without touching the data model.
        sealed class PluginDocument
        {
            public int Format { get; set; }

            public ManifestDocument Manifest { get; set; }

            /// <summary>The plugin's terrain file, next to it; absent when it has no terrain chunks.</summary>
            public string Terrain { get; set; }

            /// <summary>The plugin's objects file, next to it; absent when it has no placed objects.</summary>
            public string Objects { get; set; }

            public List<Record> Records { get; set; }

            public List<RecordId> Deletions { get; set; }

            // Reading only: the manifest as parsed, and the objects file as opened. Not written.
            [JsonIgnore]
            public PluginManifest ManifestRead { get; set; }

            [JsonIgnore]
            public IObjectBlocks ObjectBlocks { get; set; }
        }

        sealed class ManifestDocument
        {
            public string Id { get; set; }

            public string Version { get; set; }

            public List<string> Masters { get; set; }
        }
    }
}
