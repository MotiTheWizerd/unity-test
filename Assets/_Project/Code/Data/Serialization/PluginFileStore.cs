using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Forge.Data.Plugins;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Loads and saves plugins as files in a directory, one file per plugin named
    /// after its id. Saving is atomic: a crash mid-write never corrupts a plugin.
    /// </summary>
    public sealed class PluginFileStore
    {
        static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        readonly IPluginSerializer _serializer;

        public PluginFileStore(string directory, IPluginSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory is required.", nameof(directory));

            Directory = directory;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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
            var path = PathFor(plugin.Id);
            var temp = path + ".tmp";

            using (var writer = new StreamWriter(temp, false, Utf8NoBom))
                _serializer.Write(plugin, writer);

            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        Plugin LoadFile(string path)
        {
            Plugin plugin;
            try
            {
                using (var reader = new StreamReader(path, Utf8NoBom))
                    plugin = _serializer.Read(reader);
            }
            catch (DataValidationException ex)
            {
                throw new DataValidationException(ex.Errors.Select(e => $"{Path.GetFileName(path)}: {e}"), ex);
            }

            var expected = Path.GetFileNameWithoutExtension(path);
            if (plugin.Id != expected)
                throw new DataValidationException(new[] { $"{Path.GetFileName(path)}: file name must match plugin id '{plugin.Id}'." });

            return plugin;
        }
    }
}
