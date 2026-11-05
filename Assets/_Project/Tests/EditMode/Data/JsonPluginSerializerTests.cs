using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Data
{
    public class JsonPluginSerializerTests
    {
        JsonPluginSerializer _serializer;
        MemoryTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _serializer = new JsonPluginSerializer(RecordTypeRegistry.CreateDefault());
            _terrain = new MemoryTerrain();
        }

        string Write(Plugin plugin)
        {
            using (var writer = new StringWriter())
            {
                _serializer.Write(plugin, writer, MemoryTerrain.For(plugin, _terrain));
                return writer.ToString();
            }
        }

        Plugin Read(string json)
        {
            using (var reader = new StringReader(json))
                return _serializer.Read(reader, name => name == MemoryTerrain.FileName ? _terrain : throw new DataValidationException(new[] { $"No terrain file {name}." }));
        }

        static Plugin SamplePlugin() =>
            new Plugin(
                new PluginManifest("mymod", "1.2.0", new[] { "core" }),
                new Record[]
                {
                    new PlacedObjectRecord(
                        RecordId.Parse("mymod:ref/oak_1"),
                        RecordId.Parse("core:tree_oak"),
                        Space,
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
            Assert.That(placed.Space, Is.EqualTo(expected.Space));
            Assert.That(placed.Position, Is.EqualTo(expected.Position));
            Assert.That(placed.Rotation, Is.EqualTo(expected.Rotation));
            Assert.That(placed.Scale, Is.EqualTo(expected.Scale));
        }

        [Test]
        public void Output_IsDeterministicAndReadable()
        {
            var json = Write(SamplePlugin());

            Assert.That(Write(Read(json)), Is.EqualTo(json));
            StringAssert.Contains("\"format\": 2", json);
            StringAssert.Contains("\"type\": \"base_object\"", json);
            StringAssert.Contains("\"id\": \"core:tree_oak\"", json);
            StringAssert.Contains("\"baseId\": \"core:tree_oak\"", json);
            StringAssert.Contains("\"space\": \"core:space/main\"", json);
            Assert.That(json.IndexOf("core:tree_oak\""), Is.LessThan(json.IndexOf("mymod:ref/oak_1")),
                "Records are written sorted by id.");
        }

        [Test]
        public void RoundTrip_PreservesSpaces_WritingTheKindByName()
        {
            var plugin = Plugin("core",
                new SpaceRecord(Space, "Tamriel", SpaceKind.Worldspace, 50f),
                new SpaceRecord(SpaceRecord.IdFor("core", "inn"), "The Inn", SpaceKind.Interior));

            var json = Write(plugin);
            var copy = Read(json).Records.OfType<SpaceRecord>().ToDictionary(r => r.Id.ToString());

            var main = copy["core:space/main"];
            Assert.That((main.DisplayName, main.Kind, main.BaseHeight), Is.EqualTo(("Tamriel", SpaceKind.Worldspace, 50f)));
            var inn = copy["core:space/inn"];
            Assert.That((inn.DisplayName, inn.Kind, inn.BaseHeight), Is.EqualTo(("The Inn", SpaceKind.Interior, 0f)));
            StringAssert.Contains("\"type\": \"space\"", json);
            StringAssert.Contains("\"kind\": \"worldspace\"", json);
            StringAssert.Contains("\"kind\": \"interior\"", json);
            Assert.That(Write(Read(json)), Is.EqualTo(json));
        }

        [Test]
        public void RoundTrip_PreservesTheHeightmapResolution()
        {
            var plugin = Plugin("core", new SpaceRecord(Space, "Plains", SpaceKind.Worldspace, 50f, heightmapResolution: 33));

            var json = Write(plugin);
            var copy = Read(json).Records.OfType<SpaceRecord>().Single();

            Assert.That(json, Does.Contain("\"heightmapResolution\": 33"));
            Assert.That((copy.HeightmapResolution, copy.SampleSpacing), Is.EqualTo((33, 2f)));
        }

        [Test]
        public void Read_SpaceWithoutAResolution_GetsAMeter()
        {
            var json = Write(Plugin("core", new SpaceRecord(Space, "Old", SpaceKind.Worldspace, 50f)))
                .Replace("\"heightmapResolution\": 65,", string.Empty)
                .Replace(",\n      \"heightmapResolution\": 65", string.Empty)
                .Replace(",\r\n      \"heightmapResolution\": 65", string.Empty);
            Assume.That(json, Does.Not.Contain("heightmapResolution"), "The field was cut from the JSON as an older file would lack it.");

            Assert.That(Read(json).Records.OfType<SpaceRecord>().Single().HeightmapResolution, Is.EqualTo(65));
        }

        [Test]
        public void RoundTrip_PreservesTheSeaLevel_AndLeavesItOutWhenThereIsNone()
        {
            var plugin = Plugin("core",
                new SpaceRecord(Space, "Tamriel", SpaceKind.Worldspace, 50f, 47.5f),
                new SpaceRecord(OtherSpace, "Dry Land", SpaceKind.Worldspace, 50f));

            var json = Write(plugin);
            var copy = Read(json).Records.OfType<SpaceRecord>().ToDictionary(r => r.Id);

            Assert.That(copy[Space].WaterLevel, Is.EqualTo(47.5f));
            Assert.That(copy[OtherSpace].WaterLevel, Is.Null);
            Assert.That(Regex.Matches(json, "\"waterLevel\"").Count, Is.EqualTo(1), "No sea is no field, not null.");
            Assert.That(Write(Read(json)), Is.EqualTo(json));
        }

        [Test]
        public void RoundTrip_PreservesClimates_AndTheClimateOfASpace()
        {
            var climate = ClimateTests.Northern();
            var plugin = Plugin("core",
                climate,
                new SpaceRecord(Space, "Tamriel", SpaceKind.Worldspace, 50f, climate: climate.Id),
                new SpaceRecord(OtherSpace, "Plain", SpaceKind.Worldspace, 50f));

            var json = Write(plugin);
            var copy = Read(json);
            var climateCopy = copy.Records.OfType<ClimateRecord>().Single();
            var spaces = copy.Records.OfType<SpaceRecord>().ToDictionary(r => r.Id);

            Assert.That((climateCopy.DisplayName, climateCopy.YearLength), Is.EqualTo(("Northern", 120)));
            Assert.That((climateCopy.Summer.Sunrise, climateCopy.Summer.Sunset, climateCopy.Winter.NoonHeight), Is.EqualTo((4f, 22f, 20f)));
            Assert.That(climateCopy.Moons.Select(m => (m.Name, m.Period, m.PhaseAtStart, m.Size, m.Highest)),
                Is.EqualTo(climate.Moons.Select(m => (m.Name, m.Period, m.PhaseAtStart, m.Size, m.Highest))));
            Assert.That(spaces[Space].Climate, Is.EqualTo(climate.Id));
            Assert.That(spaces[OtherSpace].Climate, Is.Null);
            StringAssert.Contains("\"climate\": \"core:climate/northern\"", json);
            Assert.That(Regex.Matches(json, "\"climate\": ").Count, Is.EqualTo(1), "No climate is no field, not null.");
            Assert.That(Write(Read(json)), Is.EqualTo(json));
        }

        [Test]
        public void RoundTrip_PreservesLakes()
        {
            var lake = new LakeRecord(RecordId.Parse("core:lake/000001"), Space, 12.5f, -40f, 47.25f);

            var json = Write(Plugin("core", lake));
            var copy = Read(json).Records.OfType<LakeRecord>().Single();

            Assert.That((copy.Id, copy.Space, copy.X, copy.Z, copy.Level), Is.EqualTo((lake.Id, Space, 12.5f, -40f, 47.25f)));
            StringAssert.Contains("\"type\": \"lake\"", json);
            Assert.That(Write(Read(json)), Is.EqualTo(json));
        }

        [Test]
        public void Read_UnknownSpaceKind_IsRejected()
        {
            var json = Write(Plugin("core", new SpaceRecord(Space, "Tamriel", SpaceKind.Worldspace))).Replace("\"worldspace\"", "\"dungeon\"");

            Assert.Throws<DataValidationException>(() => Read(json));
        }

        [Test]
        public void RoundTrip_PreservesTheSpaceOfObjectsAndTerrain()
        {
            var coord = new ChunkCoord(1, -1);
            var chunkId = TerrainChunkRecord.IdFor("core", OtherSpace, coord);
            var plugin = Plugin("core",
                Base("core:rock"),
                new PlacedObjectRecord(RecordId.Parse("core:ref/a"), RecordId.Parse("core:rock"), Space, Vector3.Zero, Quaternion.Identity, 1f),
                new PlacedObjectRecord(RecordId.Parse("core:ref/b"), RecordId.Parse("core:rock"), OtherSpace, Vector3.Zero, Quaternion.Identity, 1f),
                new TerrainChunkRecord(chunkId, OtherSpace, coord, HeightField.Flat(10f, 17)));

            var copy = Read(Write(plugin)).Records.ToDictionary(r => r.Id.ToString());

            Assert.That(((PlacedObjectRecord)copy["core:ref/a"]).Space, Is.EqualTo(Space));
            Assert.That(((PlacedObjectRecord)copy["core:ref/b"]).Space, Is.EqualTo(OtherSpace));
            var chunk = (TerrainChunkRecord)copy[chunkId.ToString()];
            Assert.That((chunk.Space, chunk.Coord), Is.EqualTo((OtherSpace, coord)));
        }

        [Test]
        public void Read_PlacedObjectWithoutSpace_IsRejected()
        {
            var json = Regex.Replace(Write(SamplePlugin()), @",\s*""space"": ""core:space/main""", string.Empty);
            StringAssert.DoesNotContain("\"space\"", json);

            Assert.Throws<DataValidationException>(() => Read(json));
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
        public void RoundTrip_PreservesPlacementRules_AndOmitsThemWhenAbsent()
        {
            var plugin = Plugin("core",
                new BaseObjectRecord(RecordId.Parse("core:pine"), "Pine", "nature/pine", "Nature/Trees", new PlacementRules(true, 0.8f, 1.25f, true)),
                new BaseObjectRecord(RecordId.Parse("core:house"), "House", "architecture/house"));

            var json = Write(plugin);
            var copy = Read(json).Records.OfType<BaseObjectRecord>().ToDictionary(r => r.Id.ToString());

            var rules = copy["core:pine"].Placement;
            Assert.That(rules.RandomYaw, Is.True);
            Assert.That(rules.MinScale, Is.EqualTo(0.8f));
            Assert.That(rules.MaxScale, Is.EqualTo(1.25f));
            Assert.That(rules.AlignToSlope, Is.True);
            Assert.That(copy["core:house"].Placement, Is.Null);
            Assert.That(Regex.Matches(json, "\"placement\"").Count, Is.EqualTo(1), "No rules are written as nothing, not null.");
            StringAssert.DoesNotContain("null", json);
            StringAssert.DoesNotContain("varies", json, "Derived properties aren't data.");
        }

        [Test]
        public void Read_InvalidPlacementRules_SurfaceTheModelError()
        {
            var plugin = Plugin("core", new BaseObjectRecord(RecordId.Parse("core:pine"), "Pine", "nature/pine", null, new PlacementRules(false, 1f, 2f, false)));
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
            var json = Write(SamplePlugin()).Replace("\"format\": 2", "\"format\": 99");

            var ex = Assert.Throws<DataValidationException>(() => Read(json));
            StringAssert.Contains("Unsupported plugin format", ex.Message);
        }

        [Test]
        public void Read_Format1_ExplainsThatTerrainMovedOut()
        {
            var json = Write(SamplePlugin()).Replace("\"format\": 2", "\"format\": 1");

            var ex = Assert.Throws<DataValidationException>(() => Read(json));
            StringAssert.Contains("terrain file", ex.Message);
        }

        [Test]
        public void RoundTrip_ChunkHeights_AreABlockOfTheNamedTerrainFile_SharedBlocksOnce()
        {
            var flat = HeightField.Flat(10f, 17);
            var hill = HeightField.FromFunction((x, z) => x + z, 17);
            var plugin = Plugin("core",
                new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(0, 0)), Space, new ChunkCoord(0, 0), flat),
                new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(1, 0)), Space, new ChunkCoord(1, 0), flat),
                new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(2, 0)), Space, new ChunkCoord(2, 0), hill));

            var json = Write(plugin);
            var chunks = Read(json).Records.OfType<TerrainChunkRecord>().ToDictionary(c => c.Coord.X);

            StringAssert.Contains($"\"terrain\": \"{MemoryTerrain.FileName}\"", json);
            Assert.That(Regex.Matches(json, "\"block\": 0").Count, Is.EqualTo(2), "The two flat chunks share a block.");
            Assert.That(_terrain.BlockCount, Is.EqualTo(2));
            Assert.That(chunks[2].Heights.Load()[3, 4], Is.EqualTo(7f));
            Assert.That(chunks[0].Heights.Resolution, Is.EqualTo(17));
            Assert.That(Write(Read(json)), Is.EqualTo(json));
        }

        [Test]
        public void Read_ChunksWithoutATerrainFile_AreRejected()
        {
            var plugin = Plugin("core", new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(0, 0)), Space, new ChunkCoord(0, 0), HeightField.Flat(1f, 17)));
            var json = Regex.Replace(Write(plugin), @"\s*""terrain"": ""[^""]+"",", string.Empty);

            var ex = Assert.Throws<DataValidationException>(() => Read(json));
            StringAssert.Contains("names no terrain file", ex.Message);
        }

        [Test]
        public void Write_ChunksWithoutATerrainFile_IsRefused()
        {
            var plugin = Plugin("core", new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(0, 0)), Space, new ChunkCoord(0, 0), HeightField.Flat(1f, 17)));

            Assert.Throws<System.ArgumentException>(() => _serializer.Write(plugin, new StringWriter(), null));
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
        [TestCase("{\"format\": 2}")]
        [TestCase("{\"format\": 2, \"manifest\": {\"id\": \"x\", \"version\": \"1\"}, \"unexpected\": true}")]
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
