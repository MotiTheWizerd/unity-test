using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The materials the base game ships, for dressing placed objects' slots. Each one's asset key names its look in the
    /// material catalog (Content/MaterialCatalog), whose textures sit under Content/Materials/{folder}.
    /// </summary>
    static class CoreMaterials
    {
        public static IEnumerable<MaterialRecord> All(string pluginId)
        {
            yield return Material(pluginId, "OakPlanks", "Oak Planks");
            yield return Material(pluginId, "WeatheredPlanks", "Weathered Planks");
            yield return Material(pluginId, "Thatch", "Thatch");
            yield return Material(pluginId, "Plaster", "Plaster");
            yield return Material(pluginId, "CutStone", "Cut Stone");
            yield return Material(pluginId, "RedBrick", "Red Brick");
            yield return Material(pluginId, "RoofTiles", "Roof Tiles");
            yield return Material(pluginId, "DarkIron", "Dark Iron");
        }

        static MaterialRecord Material(string pluginId, string folder, string displayName) =>
            new MaterialRecord(MaterialRecord.IdFor(pluginId, folder.ToLowerInvariant()), displayName, MaterialBuilder.KeyFor(folder));
    }
}
