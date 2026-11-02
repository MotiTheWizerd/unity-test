using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Data
{
    public class JsonPluginSerializerTests
    {
        JsonPluginSerializer _serializer;

        [SetUp]
        public void SetUp() => _serializer = new JsonPluginSerializer(RecordTypeRegistry.CreateDefault());

        string Write(Plugin plugin)
        {
            using (var writer = new StringWriter())
            {
                _serializer.Write(plugin, writer);
                return writer.ToString();
            }
        }

        Plugin Read(string json)
        {
            using (var reader = new StringReader(json))
                return _serializer.Read(reader);
        }

        static Plugin SamplePlugin() =>
            new Plugin(
                new PluginManifest("mymod", "1.2.0", new[] { "core" }),
                new Record[]
                {
                    new PlacedObjectRecord(
                        RecordId.Parse("mymod:ref/oak_1"),
                        RecordId.Parse("core:tree_oak"),
                        new Vector3(1.5f, -2f, 3.25f),
                        Quaternion.CreateFromYawPitchRoll(0.5f, 0.1f, 0f),
                        2f),
                    new BaseObjectRecord(RecordId.Parse("core:tree_oak"), "Giant Oak", "trees/oak_giant", "Nature/Trees"),
                },
                new[] { RecordId.Parse("core:rock_01") });

        [Test]
        public void RoundTrip_PreservesEverything()
        {
            var original = SamplePlugin();

            var copy = Read(Write(original));

            Assert.That(copy.Manifest.Id, Is.EqualTo("mymod"));
            Assert.That(copy.Manifest.Version, Is.EqualTo("1.2.0"));
            Assert.That(copy.Manifest.Masters, Is.EqualTo(new[] { "core" }));
            Assert.That(copy.Deletions, Is.EqualTo(original.Deletions));

            var baseObject = (BaseObjectRecord)copy.Records.Single(r => r is BaseObjectRecord);
            Assert.That(baseObject.Id.ToString(), Is.EqualTo("core:tree_oak"));
            Assert.That(baseObject.DisplayName, Is.EqualTo("Giant Oak"));
            Assert.That(baseObject.AssetKey, Is.EqualTo("trees/oak_giant"));
            Assert.That(baseObject.Category, Is.EqualTo("Nature/Trees"));

            var expected = (PlacedObjectRecord)original.Records.Single(r => r is PlacedObjectRecord);
            var placed = (PlacedObjectRecord)copy.Records.Single(r => r is PlacedObjectRecord);
            Assert.That(placed.Id, Is.EqualTo(expected.Id));
            Assert.That(placed.BaseId, Is.EqualTo(expected.BaseId));
            Assert.That(placed.Position, Is.EqualTo(expected.Position));
            Assert.That(placed.Rotation, Is.EqualTo(expected.Rotation));
            Assert.That(placed.Scale, Is.EqualTo(expected.Scale));
        }

        [Test]
        public void Output_IsDeterministicAndReadable()
        {
            var json = Write(SamplePlugin());

            Assert.That(Write(Read(json)), Is.EqualTo(json));
            StringAssert.Contains("\"format\": 1", json);
            StringAssert.Contains("\"type\": \"base_object\"", json);
            StringAssert.Contains("\"id\": \"core:tree_oak\"", json);
            StringAssert.Contains("\"baseId\": \"core:tree_oak\"", json);
            Assert.That(json.IndexOf("core:tree_oak\""), Is.LessThan(json.IndexOf("mymod:ref/oak_1")),
                "Records are written sorted by id.");
        }

        [Test]
        public void Read_BaseObjectWithoutCategory_IsUncategorized()
        {
            // Plugins written before categories existed have no "category" field.
            var json = Regex.Replace(Write(SamplePlugin()), @",\s*""category"": ""Nature/Trees""", string.Empty);
            StringAssert.DoesNotContain("category", json);

            var baseObject = (BaseObjectRecord)Read(json).Records.Single(r => r is BaseObjectRecord);

            Assert.That(baseObject.Category, Is.Empty);
        }

        [Test]
        public void RoundTrip_PreservesPlacementVariation_AndOmitsItWhenAbsent()
        {
            var plugin = Plugin("core",
                new BaseObjectRecord(RecordId.Parse("core:pine"), "Pine", "nature/pine", "Nature/Trees", new PlacementVariation(true, 0.8f, 1.25f)),
                new BaseObjectRecord(RecordId.Parse("core:house"), "House", "architecture/house"));

            var json = Write(plugin);
            var copy = Read(json).Records.OfType<BaseObjectRecord>().ToDictionary(r => r.Id.ToString());

            var variation = copy["core:pine"].Variation;
            Assert.That(variation.RandomYaw, Is.True);
            Assert.That(variation.MinScale, Is.EqualTo(0.8f));
            Assert.That(variation.MaxScale, Is.EqualTo(1.25f));
            Assert.That(copy["core:house"].Variation, Is.Null);
            Assert.That(Regex.Matches(json, "\"variation\"").Count, Is.EqualTo(1), "No variation is written as nothing, not null.");
            StringAssert.DoesNotContain("null", json);
        }

        [Test]
        public void Read_InvalidPlacementVariation_SurfacesTheModelError()
        {
            var plugin = Plugin("core", new BaseObjectRecord(RecordId.Parse("core:pine"), "Pine", "nature/pine", null, new PlacementVariation(false, 1f, 2f)));
            var json = Write(plugin).Replace("\"maxScale\": 2.0", "\"maxScale\": 0.5");

            var ex = Assert.Throws<DataValidationException>(() => Read(json));
            StringAssert.Contains("Maximum scale", ex.Message);
        }

        [Test]
        public void Read_UnknownRecordType_IsRejected()
        {
            var json = Write(SamplePlugin()).Replace("\"base_object\"", "\"System.Diagnostics.Process\"");

            var ex = Assert.Throws<DataValidationException>(() => Read(json));
            StringAssert.Contains("Unknown record type", ex.Message);
        }

        [Test]
        public void Read_TypeNameMetadata_IsNeverHonoured()
        {
            var json = Write(SamplePlugin()).Replace(
                "\"type\": \"base_object\",",
                "\"type\": \"base_object\", \"$type\": \"System.IO.FileInfo, mscorlib\",");

            Assert.Throws<DataValidationException>(() => Read(json));
        }

        [Test]
        public void Read_UnsupportedFormatVersion_IsRejected()
        {
            var json = Write(SamplePlugin()).Replace("\"format\": 1", "\"format\": 99");

            var ex = Assert.Throws<DataValidationException>(() => Read(json));
            StringAssert.Contains("Unsupported plugin format", ex.Message);
        }

        [Test]
        public void Read_InvalidRecordData_SurfacesTheModelError()
        {
            var json = Write(SamplePlugin()).Replace("\"scale\": 2.0", "\"scale\": -1.0");

            var ex = Assert.Throws<DataValidationException>(() => Read(json));
            StringAssert.Contains("Scale must be", ex.Message);
        }

        [Test]
        public void Read_PluginRuleViolation_IsRejected()
        {
            // Renaming the plugin leaves its "mymod:" records in a namespace it no longer owns.
            var json = Write(SamplePlugin()).Replace("\"id\": \"mymod\"", "\"id\": \"othermod\"");

            var ex = Assert.Throws<DataValidationException>(() => Read(json));
            StringAssert.Contains("mymod:ref/oak_1", ex.Message);
        }

        [TestCase("")]
        [TestCase("{ not json")]
        [TestCase("{\"format\": 1}")]
        [TestCase("{\"format\": 1, \"manifest\": {\"id\": \"x\", \"version\": \"1\"}, \"unexpected\": true}")]
        public void Read_MalformedInput_IsRejected(string json)
        {
            Assert.Throws<DataValidationException>(() => Read(json));
        }

        [Test]
        public void SavedPlugins_ResolveLikeInMemoryOnes()
        {
            var core = Read(Write(Plugin("core", Base("core:tree_oak", "Oak"), Base("core:rock_01"))));
            var mod = Read(Write(SamplePlugin()));

            var world = LoadOrderResolver.Resolve(new[] { core, mod });

            Assert.That(world.Records.Get<BaseObjectRecord>(RecordId.Parse("core:tree_oak")).DisplayName, Is.EqualTo("Giant Oak"));
            Assert.That(world.Records.Contains(RecordId.Parse("core:rock_01")), Is.False);
            Assert.That(world.Records.Count, Is.EqualTo(2));
        }
    }
}
