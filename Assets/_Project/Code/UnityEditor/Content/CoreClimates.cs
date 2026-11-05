using System.Collections.Generic;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Sky;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// The climates the base game ships: a temperate one (the main world's), a northern one with white summer nights
    /// and short winter days, and a desert one with a high sun all year. All share the default moons and a 120-day
    /// year, 30 days a season.
    /// </summary>
    static class CoreClimates
    {
        const int YearLength = 120;

        public static RecordId Temperate(string pluginId) => ClimateRecord.IdFor(pluginId, "temperate");

        public static IEnumerable<ClimateRecord> All(string pluginId)
        {
            yield return new ClimateRecord(Temperate(pluginId), "Temperate", YearLength,
                summer: new DayCycle(5f, 21f, 62f), winter: new DayCycle(8f, 17f, 25f), Moon.Defaults);
            yield return new ClimateRecord(ClimateRecord.IdFor(pluginId, "northern"), "Northern", YearLength,
                summer: new DayCycle(3f, 23f, 50f), winter: new DayCycle(10f, 15f, 8f), Moon.Defaults);
            yield return new ClimateRecord(ClimateRecord.IdFor(pluginId, "desert"), "Desert", YearLength,
                summer: new DayCycle(5.5f, 20f, 80f), winter: new DayCycle(7f, 18f, 50f), Moon.Defaults);
        }
    }
}
