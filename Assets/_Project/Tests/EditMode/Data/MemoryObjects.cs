using System.Collections.Generic;
using Forge.Data.Plugins;
using Forge.Data.Serialization;

namespace Forge.Tests.Data
{
    /// <summary>An objects file kept in memory, for serializer tests: the blocks of each plugin written, by file name.</summary>
    sealed class MemoryObjects
    {
        public const string FileName = "test.0000000000000000.objects";

        readonly Dictionary<string, IObjectBlocks> _files = new Dictionary<string, IObjectBlocks>();

        /// <summary>An objects file holding <paramref name="plugin"/>'s objects, or null when it has none.</summary>
        public ObjectsLink For(Plugin plugin)
        {
            if (plugin.Objects.TotalCount == 0)
                return null;
            _files[FileName] = plugin.Objects;
            return new ObjectsLink(FileName);
        }

        public IObjectBlocks Open(string name, PluginManifest manifest) =>
            _files.TryGetValue(name, out var blocks) ? blocks : throw new Forge.Data.DataValidationException(new[] { $"No objects file {name}." });
    }
}
