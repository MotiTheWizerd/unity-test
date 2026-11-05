using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Loads and saves plugins as files in a directory: one file per plugin named after its id, plus, next to it,
    /// a terrain file for its chunk heights and an objects file for its placed objects, each named after the plugin
    /// id and the hash of its content (<c>forge.user.1a2b….terrain</c>, <c>forge.user.3c4d….objects</c>) and named by
    /// the plugin file. Loading reads only the side files' tables; heights and objects are read when needed.
    /// </summary>
    /// <remarks>
    /// Saving never leaves a plugin broken, even if it stops halfway. The side files are written first, under names
    /// no other content has. Then the plugin file, which names them, replaces the old one in a single atomic step:
    /// the commit point. Only then are older side files of the plugin removed, except the ones it was loaded with,
    /// whose blocks the open world may still read.
    /// </remarks>
    public sealed class PluginFileStore
    {
        static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        readonly IPluginSerializer _serializer;
        readonly HeightCache _heights;
        readonly ObjectCache _objects;

        // The side files each plugin was loaded with, by plugin id: saving keeps them, as their blocks may still be read.
        readonly Dictionary<string, string> _loadedTerrain = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _loadedObjects = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <param name="heights">Where loaded heights are kept; share one across a world's stores so one budget covers it.</param>
        /// <param name="objects">Where loaded objects are kept; share one across a world's stores so one budget covers it.</param>
        public PluginFileStore(string directory, IPluginSerializer serializer, HeightCache heights = null, ObjectCache objects = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory is required.", nameof(directory));

            Directory = directory;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _heights = heights ?? new HeightCache();
            _objects = objects ?? new ObjectCache();
        }

        public string Directory { get; }

        public string PathFor(string pluginId) => Path.Combine(Directory, pluginId + _serializer.FileExtension);

        public bool Exists(string pluginId) => File.Exists(PathFor(pluginId));

        public Plugin Load(string pluginId) => LoadFile(PathFor(pluginId));

        /// <summary>Loads every plugin file in the directory; a missing directory yields none.</summary>
        public IReadOnlyList<Plugin> LoadAll()
        {
            if (!System.IO.Directory.Exists(Directory))
                return Array.Empty<Plugin>();

            return System.IO.Directory
                .GetFiles(Directory, "*" + _serializer.FileExtension)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(LoadFile)
                .ToArray();
        }

        public void Save(Plugin plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            System.IO.Directory.CreateDirectory(Directory);
            var terrain = SaveTerrain(plugin);
            var objects = SaveObjects(plugin);

            var path = PathFor(plugin.Id);
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, Utf8NoBom, 4096, leaveOpen: true))
                    _serializer.Write(plugin, writer, terrain, objects);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);

            RemoveOldSideFiles(plugin.Id, TerrainFile.Extension, terrain?.FileName, _loadedTerrain);
            RemoveOldSideFiles(plugin.Id, ObjectsFile.Extension, objects?.FileName, _loadedObjects);
        }

        /// <summary>The terrain file name of plugin <paramref name="pluginId"/> whose content hashes to <paramref name="hash"/>.</summary>
        public static string TerrainFileName(string pluginId, string hash) => SideFileName(pluginId, hash, TerrainFile.Extension);

        /// <summary>The objects file name of plugin <paramref name="pluginId"/> whose content hashes to <paramref name="hash"/>.</summary>
        public static string ObjectsFileName(string pluginId, string hash) => SideFileName(pluginId, hash, ObjectsFile.Extension);

        /// <summary>Whether <paramref name="fileName"/> is a terrain file of plugin <paramref name="pluginId"/>.</summary>
        public static bool IsTerrainFileOf(string pluginId, string fileName) => IsSideFileOf(pluginId, fileName, TerrainFile.Extension);

        /// <summary>Whether <paramref name="fileName"/> is an objects file of plugin <paramref name="pluginId"/>.</summary>
        public static bool IsObjectsFileOf(string pluginId, string fileName) => IsSideFileOf(pluginId, fileName, ObjectsFile.Extension);

        static string SideFileName(string pluginId, string hash, string extension) => $"{pluginId}.{hash}{extension}";

        // Both side files name themselves the same way, with hashes of the same length.
        static bool IsSideFileOf(string pluginId, string fileName, string extension)
        {
            var prefix = pluginId + ".";
            if (fileName == null || !fileName.StartsWith(prefix, StringComparison.Ordinal) || !fileName.EndsWith(extension, StringComparison.Ordinal))
                return false;

            var hash = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - extension.Length);
            return hash.Length == WrittenTerrain.HashLength && hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
        }

        // Writes the plugin's chunk heights to a terrain file named by its content; null when it has no terrain.
        TerrainLink SaveTerrain(Plugin plugin)
        {
            var chunks = plugin.Records.OfType<TerrainChunkRecord>().ToList();
            if (chunks.Count == 0)
                return null;

            var written = WriteSideFile(plugin.Id, TerrainFile.Extension, stream => TerrainFileWriter.Write(stream, chunks.Select(c => c.Heights)), w => w.Hash, out var name);
            return new TerrainLink(name, written);
        }

        // Writes the plugin's placed objects to an objects file named by its content; null when it has none.
        ObjectsLink SaveObjects(Plugin plugin)
        {
            if (plugin.Objects.TotalCount == 0)
                return null;

            WriteSideFile(plugin.Id, ObjectsFile.Extension, stream => ObjectsFileWriter.Write(stream, plugin.Objects), w => w.Hash, out var name);
            return new ObjectsLink(name);
        }

        // Writes a side file to a temporary name, then gives it its content's name; the same name means the same
        // content, already saved.
        TWritten WriteSideFile<TWritten>(string pluginId, string extension, Func<Stream, TWritten> write, Func<TWritten, string> hashOf, out string name)
        {
            var temp = Path.Combine(Directory, pluginId + extension + ".tmp");
            TWritten written;
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                written = write(stream);
                stream.Flush(flushToDisk: true);
            }

            name = SideFileName(pluginId, hashOf(written), extension);
            var path = Path.Combine(Directory, name);
            if (File.Exists(path))
                File.Delete(temp);
            else
                File.Move(temp, path);
            return written;
        }

        // Removes the plugin's side files of one kind other than the one just saved and the one it was loaded with.
        // Best effort: a file another process has open stays, and goes on a later save.
        void RemoveOldSideFiles(string pluginId, string extension, string saved, Dictionary<string, string> loadedByPlugin)
        {
            loadedByPlugin.TryGetValue(pluginId, out var loaded);
            foreach (var path in System.IO.Directory.GetFiles(Directory, pluginId + ".*" + extension))
            {
                var name = Path.GetFileName(path);
                if (!IsSideFileOf(pluginId, name, extension) || name == saved || name == loaded)
                    continue;

                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Left for a later save.
                }
            }
        }

        Plugin LoadFile(string path)
        {
            var fileName = Path.GetFileName(path);
            string terrainName = null;
            string objectsName = null;
            Plugin plugin;
            try
            {
                using (var reader = new StreamReader(path, Utf8NoBom))
                    plugin = _serializer.Read(reader, name => OpenTerrain(terrainName = name), (name, manifest) => OpenObjects(objectsName = name, manifest));
            }
            catch (DataValidationException ex)
            {
                throw new DataValidationException(ex.Errors.Select(e => $"{fileName}: {e}"), ex);
            }

            var expected = Path.GetFileNameWithoutExtension(path);
            if (plugin.Id != expected)
                throw new DataValidationException(new[] { $"{fileName}: file name must match plugin id '{plugin.Id}'." });
            if (terrainName != null)
            {
                if (!IsTerrainFileOf(plugin.Id, terrainName))
                    throw new DataValidationException(new[] { $"{fileName}: terrain file '{terrainName}' is not one of {plugin.Id}'s." });
                _loadedTerrain[plugin.Id] = terrainName;
            }

            if (objectsName != null)
            {
                if (!IsObjectsFileOf(plugin.Id, objectsName))
                    throw new DataValidationException(new[] { $"{fileName}: objects file '{objectsName}' is not one of {plugin.Id}'s." });
                _loadedObjects[plugin.Id] = objectsName;
            }

            return plugin;
        }

        // Only a file next to the plugin, never a path elsewhere: plugin files are untrusted input.
        ITerrainBlockReader OpenTerrain(string name)
        {
            CheckSideFileName(name, TerrainFile.Extension, "Terrain");
            return TerrainFile.Open(Path.Combine(Directory, name), _heights);
        }

        IObjectBlocks OpenObjects(string name, PluginManifest manifest)
        {
            CheckSideFileName(name, ObjectsFile.Extension, "Objects");
            return ObjectsFile.Open(Path.Combine(Directory, name), manifest, _objects);
        }

        static void CheckSideFileName(string name, string extension, string kind)
        {
            if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || !name.EndsWith(extension, StringComparison.Ordinal))
                throw new DataValidationException(new[] { $"{kind} file '{name}' must be a {extension} file next to the plugin." });
        }
    }
}
