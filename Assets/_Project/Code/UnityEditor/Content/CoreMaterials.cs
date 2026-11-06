using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The materials the base game ships, for dressing placed objects' slots. Each one's asset key names its look in the
    /// material catalog (Content/MaterialCatalog), whose textures sit under Content/Materials/{folder}; its category is
    /// the kind of surface, which is the library tab it shows under.
    /// </summary>
    static class CoreMaterials
    {
        const string Wood = "Wood";
        const string Stone = "Stone";
        const string Brick = "Brick";
        const string Plaster = "Plaster";
        const string Roofing = "Roofing";
        const string Metal = "Metal";

        public static IEnumerable<MaterialRecord> All(string pluginId)
        {
            yield return Material(pluginId, "OakPlanks", "Oak Planks", Wood);
            yield return Material(pluginId, "WeatheredPlanks", "Weathered Planks", Wood);
            yield return Material(pluginId, "Thatch", "Thatch", Roofing);
            yield return Material(pluginId, "Plaster", "Plaster", Plaster);
            yield return Material(pluginId, "CutStone", "Cut Stone", Stone);
            yield return Material(pluginId, "RedBrick", "Red Brick", Brick);
            yield return Material(pluginId, "RoofTiles", "Roof Tiles", Roofing);
            yield return Material(pluginId, "DarkIron", "Dark Iron", Metal);
        }

        static MaterialRecord Material(string pluginId, string folder, string displayName, string category) =>
            new MaterialRecord(MaterialRecord.IdFor(pluginId, folder.ToLowerInvariant()), displayName, MaterialBuilder.KeyFor(folder), category);
    }
}
