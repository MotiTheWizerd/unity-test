using System.Collections.Generic;
using Forge.Data.Records;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The ground layers the base game ships. Each one's asset key names its look in the ground layer catalog
    /// (Content/GroundLayerCatalog), whose textures sit under Content/Ground.
    /// </summary>
    static class CoreGroundLayers
    {
        public static IEnumerable<GroundLayerRecord> All(string pluginId)
        {
            yield return Layer(pluginId, "grass", "Grass", new GrassSettings(density: 5f, height: 0.5f));
            yield return Layer(pluginId, "dirt", "Dirt");
            yield return Layer(pluginId, "rock", "Rock");
            yield return Layer(pluginId, "sand", "Sand");
            yield return Layer(pluginId, "snow", "Snow");
            yield return Layer(pluginId, "gravel", "Gravel");
            yield return Layer(pluginId, "mud", "Mud");
            yield return Layer(pluginId, "forestfloor", "Forest Floor", new GrassSettings(density: 0.6f, height: 0.3f));
            yield return Layer(pluginId, "moss", "Moss");
            yield return Layer(pluginId, "drygrass", "Dry Grass", new GrassSettings(density: 2.5f, height: 0.4f));
        }

        static GroundLayerRecord Layer(string pluginId, string name, string displayName, GrassSettings grass = null) =>
            new GroundLayerRecord(GroundLayerRecord.IdFor(pluginId, name), displayName, GroundLayerRecord.NamePrefix + name, grass);
    }
}
