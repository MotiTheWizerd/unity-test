using System;
using System.IO;
using Forge.Data.Plugins;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Reads and writes plugins in a specific file format. Implementations live in
    /// their own assemblies (e.g. Forge.Data.Json) so the data model stays
    /// independent of any serialization library. Terrain heights are not part of the format: they go to the plugin's
    /// terrain file (<see cref="TerrainFile"/>), which the plugin names and refers to by block.
    /// </summary>
    public interface IPluginSerializer
    {
        /// <summary>File extension for this format, including the dot, e.g. ".json".</summary>
        string FileExtension { get; }

        /// <param name="terrain">Where the plugin's chunk heights were written; null when it has no terrain chunks.</param>
        void Write(Plugin plugin, TextWriter writer, TerrainLink terrain);

        /// <param name="openTerrain">Opens the terrain file the plugin names (a file name, no folder).</param>
        /// <exception cref="DataValidationException">The content is malformed or violates plugin rules.</exception>
        Plugin Read(TextReader reader, Func<string, ITerrainBlockReader> openTerrain);
    }
}
