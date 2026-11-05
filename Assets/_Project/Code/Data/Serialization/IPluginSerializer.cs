using System;
using System.IO;
using Forge.Data.Plugins;

namespace Forge.Data.Serialization
{
    /// <summary>
    /// Reads and writes plugins in some file format. The format carries the manifest, the records and the deletions;
    /// terrain heights and placed objects go to side files the plugin names (a terrain file and an objects file).
    /// </summary>
    public interface IPluginSerializer
    {
        /// <summary>File extension for this format, including the dot, e.g. ".json".</summary>
        string FileExtension { get; }

        /// <param name="terrain">Where the plugin's chunk heights were written; null when it has no terrain chunks.</param>
        /// <param name="objects">Where the plugin's placed objects were written; null when it has none.</param>
        void Write(Plugin plugin, TextWriter writer, TerrainLink terrain, ObjectsLink objects);

        /// <param name="openTerrain">Opens the terrain file the plugin names (a file name, no folder).</param>
        /// <param name="openObjects">Opens the objects file the plugin names (a file name, no folder), for the plugin's manifest.</param>
        /// <exception cref="DataValidationException">The content is malformed or violates plugin rules.</exception>
        Plugin Read(TextReader reader, Func<string, ITerrainBlockReader> openTerrain, Func<string, PluginManifest, IObjectBlocks> openObjects);
    }
}
