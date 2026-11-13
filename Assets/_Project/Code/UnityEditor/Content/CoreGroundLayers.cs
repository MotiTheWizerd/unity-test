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
        // What each is underfoot, which its footsteps are found by (Content/Sounds/Footsteps/<Surface>).
        const string Grass = Surfaces.Grass, Dirt = Surfaces.Dirt, Stone = Surfaces.Stone, Sand = Surfaces.Sand, Snow = Surfaces.Snow,
            Gravel = Surfaces.Gravel, Wet = Surfaces.Wet;

        public static IEnumerable<GroundLayerRecord> All(string pluginId)
        {
            yield return Layer(pluginId, "grass", "Grass", Grass, new GrassSettings(density: 5f, height: 0.5f));
            yield return Layer(pluginId, "dirt", "Dirt", Dirt);
            yield return Layer(pluginId, "rock", "Rock", Stone);
            yield return Layer(pluginId, "sand", "Sand", Sand);
            yield return Layer(pluginId, "snow", "Snow", Snow);
            yield return Layer(pluginId, "gravel", "Gravel", Gravel);
            yield return Layer(pluginId, "mud", "Mud", Wet);
            yield return Layer(pluginId, "forestfloor", "Forest Floor", Dirt, new GrassSettings(density: 0.6f, height: 0.3f));
            yield return Layer(pluginId, "moss", "Moss", Grass);
            yield return Layer(pluginId, "drygrass", "Dry Grass", Grass, new GrassSettings(density: 2.5f, height: 0.4f));
            // Town ground (Poly Haven): streets, yards and squares.
            yield return Layer(pluginId, "cobblestone", "Cobblestone", Stone);
            yield return Layer(pluginId, "flagstone", "Flagstone", Stone);
            yield return Layer(pluginId, "packedearth", "Packed Earth", Dirt);
            yield return Layer(pluginId, "wetmud", "Wet Mud", Wet);
            // Their variants (Poly Haven), so a street isn't one ground end to end: grass between the stones at its edges,
            // a worn and rutted stretch, a trodden path through grass, stony mud where it's wet. The grassy two
            // grow short, sparse blades, so grass stands between the stones and along the path.
            yield return Layer(pluginId, "grassycobblestone", "Grassy Cobblestone", Stone, new GrassSettings(density: 1.2f, height: 0.25f));
            yield return Layer(pluginId, "worncobblestone", "Worn Cobblestone", Stone);
            yield return Layer(pluginId, "grasspath", "Grass Path", Dirt, new GrassSettings(density: 2f, height: 0.3f));
            yield return Layer(pluginId, "stonymud", "Stony Mud", Wet);
            // A road of flat stones laid in earth (Poly Haven, 2K: seen underfoot), the kind that runs between towns. Its
            // stones stand high in its height map, so at a road's soft edge they thin out into the ground beside it.
            yield return Layer(pluginId, "stoneroad", "Stone Road", Stone);
            // The other roads (Poly Haven, 2K), each the worn middle of its kind of road (the roads guide lays them):
            // packed earth and small stones, a trodden forest floor, loose mountain scree, snow trodden through to earth.
            yield return Layer(pluginId, "dirtroad", "Dirt Road", Dirt);
            yield return Layer(pluginId, "foresttrail", "Forest Trail", Dirt);
            yield return Layer(pluginId, "mountainroad", "Mountain Road", Gravel);
            yield return Layer(pluginId, "snowyroad", "Snowy Road", Snow);
        }

        static GroundLayerRecord Layer(string pluginId, string name, string displayName, string surface, GrassSettings grass = null) =>
            new GroundLayerRecord(GroundLayerRecord.IdFor(pluginId, name), displayName, GroundLayerRecord.NamePrefix + name, grass, surface);
    }
}
