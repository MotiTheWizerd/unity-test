using System.IO;
using Forge.Data.Plugins;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Reads and writes plugins in a specific file format. Implementations live in
    /// their own assemblies (e.g. Forge.Data.Json) so the data model stays
    /// independent of any serialization library.
    /// </summary>
    public interface IPluginSerializer
    {
        /// <summary>File extension for this format, including the dot, e.g. ".json".</summary>
        string FileExtension { get; }

        void Write(Plugin plugin, TextWriter writer);

        /// <exception cref="DataValidationException">The content is malformed or violates plugin rules.</exception>
        Plugin Read(TextReader reader);
    }
}
