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
    /// Loads and saves plugins as files in a directory: one file per plugin named after its id, plus, for a plugin
    /// with terrain, a terrain file named after its id and the hash of its content (<c>forge.user.1a2b….terrain</c>)
    /// that the plugin file names. Loading reads only the terrain files' block tables; heights are read when needed.
    /// </summary>
    /// <remarks>
    /// Saving never leaves a plugin broken, even if it stops halfway. The terrain file is written first, under a name
    /// no other content has. Then the plugin file, which names it, replaces the old one in a single atomic step: the
    /// commit point. Only then are older terrain files of the plugin removed, except the one it was loaded with,
    /// whose blocks the open world may still read.
    /// </remarks>
    public sealed class PluginFileStore
    {
        static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        readonly IPluginSerializer _serializer;
        readonly HeightCache _cache;

        // The terrain file each plugin was loaded with, by plugin id: saving keeps it, as its chunks may still be read.
        readonly Dictionary<string, string> _loadedTerrain = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <param name="cache">Where loaded heights are kept; share one across a world's stores so one budget covers it.</param>
        public PluginFileStore(string directory, IPluginSerializer serializer, HeightCache cache = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory is required.", nameof(directory));

            Directory = directory;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _cache = cache ?? new HeightCache();
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

            var path = PathFor(plugin.Id);
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, Utf8NoBom, 4096, leaveOpen: true))
                    _serializer.Write(plugin, writer, terrain);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);

            RemoveOldTerrain(plugin.Id, terrain?.FileName);
        }

        /// <summary>The terrain file name of plugin <paramref name="pluginId"/> whose content hashes to <paramref name="hash"/>.</summary>
        public static string TerrainFileName(string pluginId, string hash) => $"{pluginId}.{hash}{TerrainFile.Extension}";

        /// <summary>Whether <paramref name="fileName"/> is a terrain file of plugin <paramref name="pluginId"/>.</summary>
        public static bool IsTerrainFileOf(string pluginId, string fileName)
        {
            var prefix = pluginId + ".";
            if (fileName == null || !fileName.StartsWith(prefix, StringComparison.Ordinal) || !fileName.EndsWith(TerrainFile.Extension, StringComparison.Ordinal))
                return false;

            var hash = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - TerrainFile.Extension.Length);
            return hash.Length == WrittenTerrain.HashLength && hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
        }

        // Writes the plugin's chunk heights to a terrain file named by its content; null when it has no terrain.
        TerrainLink SaveTerrain(Plugin plugin)
        {
            var chunks = plugin.Records.OfType<TerrainChunkRecord>().ToList();
            if (chunks.Count == 0)
                return null;

            var temp = Path.Combine(Directory, plugin.Id + TerrainFile.Extension + ".tmp");
            WrittenTerrain written;
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                written = TerrainFileWriter.Write(stream, chunks.Select(c => c.Heights));
                stream.Flush(flushToDisk: true);
            }

            var name = TerrainFileName(plugin.Id, written.Hash);
            var path = Path.Combine(Directory, name);
            // The same name means the same content, already saved.
            if (File.Exists(path))
                File.Delete(temp);
            else
                File.Move(temp, path);
            return new TerrainLink(name, written);
        }

        // Removes the plugin's terrain files other than the one just saved and the one it was loaded with. Best effort:
        // a file another process has open stays, and goes on a later save.
        void RemoveOldTerrain(string pluginId, string saved)
        {
            _loadedTerrain.TryGetValue(pluginId, out var loaded);
            foreach (var path in System.IO.Directory.GetFiles(Directory, pluginId + ".*" + TerrainFile.Extension))
            {
                var name = Path.GetFileName(path);
                if (!IsTerrainFileOf(pluginId, name) || name == saved || name == loaded)
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
            Plugin plugin;
            try
            {
                using (var reader = new StreamReader(path, Utf8NoBom))
                    plugin = _serializer.Read(reader, name => OpenTerrain(terrainName = name));
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

            return plugin;
        }

        // Only a file next to the plugin, never a path elsewhere: plugin files are untrusted input.
        ITerrainBlockReader OpenTerrain(string name)
        {
            if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || !name.EndsWith(TerrainFile.Extension, StringComparison.Ordinal))
                throw new DataValidationException(new[] { $"Terrain file '{name}' must be a {TerrainFile.Extension} file next to the plugin." });

            return TerrainFile.Open(Path.Combine(Directory, name), _cache);
        }
    }
}
