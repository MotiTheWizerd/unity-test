using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;

namespace Forge.Tests.Data
{
    /// <summary>Compact builders for test data.</summary>
    static class TestRecords
    {
        /// <summary>The space test objects and terrain stand in, unless a test is about another one.</summary>
        public static readonly RecordId Space = SpaceRecord.IdFor("core", "main");

        /// <summary>A second space, for checking that records of one space stay out of another.</summary>
        public static readonly RecordId OtherSpace = SpaceRecord.IdFor("core", "other");

        public static BaseObjectRecord Base(string id, string displayName = "Thing") =>
            new BaseObjectRecord(RecordId.Parse(id), displayName, "assets/thing");

        public static PlacedObjectRecord Placed(string id, string baseId, float x = 0f) =>
            new PlacedObjectRecord(RecordId.Parse(id), RecordId.Parse(baseId), Space, new Vector3(x, 0f, 0f), Quaternion.Identity, 1f);

        public static Plugin Plugin(string id, string[] masters, params Record[] records) =>
            new Plugin(new PluginManifest(id, "1.0.0", masters), records);

        public static Plugin Plugin(string id, params Record[] records) =>
            Plugin(id, null, records);

        public static Plugin Deleting(string id, string[] masters, params string[] deletedIds) =>
            new Plugin(new PluginManifest(id, "1.0.0", masters), null, System.Array.ConvertAll(deletedIds, RecordId.Parse));
    }
}
