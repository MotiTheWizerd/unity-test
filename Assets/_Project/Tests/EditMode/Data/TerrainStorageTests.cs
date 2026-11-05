using System;
using System.IO;
using System.Linq;
using Forge.Data;
using Forge.Data.Json;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Serialization;
using Forge.Data.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Data
{
    /// <summary>Terrain heights on disk: the codec, the terrain file, the height cache, and plugins saved with their terrain.</summary>
    public class TerrainStorageTests
    {
        // Half a height step: the most a stored height can be off.
        const float Precision = TerrainFileWriter.MetersPerStep / 2f + 1e-5f;

        string _directory;
        PluginFileStore _store;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "forge-terrain-" + Guid.NewGuid().ToString("N"));
            _store = Store();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        PluginFileStore Store(HeightCache cache = null) =>
            new PluginFileStore(_directory, new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()), cache);

        static HeightField Hills(int resolution = 65) =>
            HeightField.FromFunction((x, z) => 50f + 20f * (float)Math.Sin(x * 0.2) * (float)Math.Cos(z * 0.15) + x * 0.37f, resolution);

        static TerrainChunkRecord Chunk(string ns, int x, ChunkHeights heights) =>
            new TerrainChunkRecord(TerrainChunkRecord.IdFor(ns, Space, new ChunkCoord(x, 0)), Space, new ChunkCoord(x, 0), heights);

        static void AssertClose(HeightField actual, HeightField expected)
        {
            var a = actual.ToArray();
            var e = expected.ToArray();
            Assert.That(a.Length, Is.EqualTo(e.Length));
            Assert.That(a.Zip(e, (x, y) => Math.Abs(x - y)).Max(), Is.LessThanOrEqualTo(Precision));
        }

        string[] TerrainFiles() => Directory.GetFiles(_directory, "*" + TerrainFile.Extension).Select(Path.GetFileName).OrderBy(n => n).ToArray();

        [Test]
        public void Codec_KeepsHeightsWithinHalfAStep_AndIsStableWhenSavedAgain()
        {
            var hills = Hills();

            var once = HeightCodecRoundTrip(hills);
            var twice = HeightCodecRoundTrip(once);

            AssertClose(once, hills);
            Assert.That(twice.ToArray(), Is.EqualTo(once.ToArray()), "Saving loaded heights again changes nothing.");
        }

        [Test]
        public void Codec_KeepsRoundHeightsExact()
        {
            var round = HeightField.FromFunction((x, z) => (x + z) % 3 == 0 ? 50f : 47.5f, 17);

            Assert.That(HeightCodecRoundTrip(round).ToArray(), Is.EqualTo(round.ToArray()));
        }

        [Test]
        public void Codec_PacksTheEdgesOfTheRange_AndFlatLandToAlmostNothing()
        {
            var edges = HeightField.FromFunction((x, z) => (x + z) % 2 == 0 ? 0f : WorldGrid.MaxTerrainHeight, 17);
            AssertClose(HeightCodecRoundTrip(edges), edges);
            Assert.That(HeightCodecRoundTrip(edges).ToArray().Max(), Is.LessThanOrEqualTo(WorldGrid.MaxTerrainHeight));

            Assert.That(TerrainFileSize(HeightField.Flat(50f)), Is.LessThan(200), "65 × 65 flat samples.");
            Assert.That(TerrainFileSize(Hills()), Is.LessThan(65 * 65 * 2), "Smooth hills pack below 2 bytes a sample.");
        }

        [Test]
        public void File_SharedHeights_AreOneBlock_AndOpeningReadsOnlyTheTable()
        {
            var flat = HeightField.Flat(50f);
            var path = Path.Combine(_directory, "t.terrain");
            Directory.CreateDirectory(_directory);
            WrittenTerrain written;
            using (var stream = File.Create(path))
                written = TerrainFileWriter.Write(stream, new ChunkHeights[] { flat, Hills(), flat });

            var cache = new HeightCache();
            var file = TerrainFile.Open(path, cache);

            Assert.That((written.BlockCount, file.BlockCount), Is.EqualTo((2, 2)));
            Assert.That(written.BlockOf(flat), Is.EqualTo(0));
            Assert.Throws<ArgumentException>(() => written.BlockOf(Hills()), "Blocks are found by instance, not by content.");
            Assert.That(cache.Count, Is.EqualTo(0), "Nothing decoded yet.");
            AssertClose(file.Block(1, 65).Load(), Hills());
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(file.Block(0, 65), Is.SameAs(file.Block(0, 65)), "One handle per block.");
        }

        [Test]
        public void File_SameContent_SameHash_DifferentContent_DifferentHash()
        {
            Assert.That(HashOf(Hills()), Is.EqualTo(HashOf(Hills())));
            Assert.That(HashOf(Hills()), Is.Not.EqualTo(HashOf(HeightField.Flat(50f))));
            Assert.That(HashOf(Hills()), Has.Length.EqualTo(WrittenTerrain.HashLength));
        }

        [TestCase("not a terrain file at all")]
        [TestCase("")]
        public void File_ThatIsNotTerrain_IsRejected(string content)
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "bad.terrain");
            File.WriteAllText(path, content);

            Assert.Throws<DataValidationException>(() => TerrainFile.Open(path, new HeightCache()));
        }

        [Test]
        public void File_CutShort_OrMissing_IsRejected()
        {
            var path = WriteFile(Hills());
            var bytes = File.ReadAllBytes(path);
            File.WriteAllBytes(path, bytes.Take(bytes.Length - 3).ToArray());

            Assert.Throws<DataValidationException>(() => TerrainFile.Open(path, new HeightCache()));
            Assert.Throws<DataValidationException>(() => TerrainFile.Open(Path.Combine(_directory, "gone.terrain"), new HeightCache()));
        }

        [Test]
        public void File_DamagedBlock_IsRejectedWhenRead()
        {
            var path = WriteFile(Hills());
            var bytes = File.ReadAllBytes(path);
            for (var i = TerrainFile.HeaderSize; i < TerrainFile.HeaderSize + 40; i++)
                bytes[i] ^= 0x5A;
            File.WriteAllBytes(path, bytes);

            var file = TerrainFile.Open(path, new HeightCache());
            Assert.Throws<DataValidationException>(() => file.Block(0, 65).Load());
        }

        [Test]
        public void File_AsksForABlockItLacks_OrAtAnotherResolution_AreRejected()
        {
            var file = TerrainFile.Open(WriteFile(Hills()), new HeightCache());

            Assert.Throws<DataValidationException>(() => file.Block(1, 65));
            Assert.Throws<DataValidationException>(() => file.Block(0, 33));
        }

        [Test]
        public void Cache_LetsGoOfTheLeastRecentlyUsed_OverBudget_AndReadsThemAgain()
        {
            var oneChunk = 65L * 65 * sizeof(float);
            var cache = new HeightCache(oneChunk * 2);
            var loads = 0;
            HeightField Load() { loads++; return HeightField.Flat(1f); }
            object a = new object(), b = new object(), c = new object();

            cache.GetOrLoad(a, Load);
            cache.GetOrLoad(b, Load);
            cache.GetOrLoad(a, Load);
            cache.GetOrLoad(c, Load);

            Assert.That(loads, Is.EqualTo(3));
            Assert.That((cache.Count, cache.UsedBytes), Is.EqualTo((2, oneChunk * 2)));
            cache.GetOrLoad(a, Load);
            Assert.That(loads, Is.EqualTo(3), "a was used recently, so b went.");
            cache.GetOrLoad(b, Load);
            Assert.That(loads, Is.EqualTo(4));
        }

        [Test]
        public void Store_SavesHeightsBesideThePlugin_AndLoadsThemOnDemand()
        {
            var hills = Hills();
            _store.Save(Plugin("core", Chunk("core", 0, hills), Chunk("core", 1, HeightField.Flat(50f))));

            var cache = new HeightCache();
            var loaded = Store(cache).Load("core");
            var chunks = loaded.Records.OfType<TerrainChunkRecord>().ToDictionary(c => c.Coord.X);

            var file = TerrainFiles().Single();
            Assert.That(PluginFileStore.IsTerrainFileOf("core", file), Is.True);
            StringAssert.Contains($"\"terrain\": \"{file}\"", File.ReadAllText(_store.PathFor("core")));
            Assert.That(cache.Count, Is.EqualTo(0), "Loading a plugin reads no heights.");
            AssertClose(chunks[0].Heights.Load(), hills);
            Assert.That(chunks[1].Heights.Load()[10, 10], Is.EqualTo(50f).Within(Precision));
        }

        [Test]
        public void Store_PluginWithoutTerrain_HasNoTerrainFile()
        {
            _store.Save(Plugin("core", Base("core:tree", "Oak")));

            Assert.That(TerrainFiles(), Is.Empty);
            StringAssert.DoesNotContain("\"terrain\"", File.ReadAllText(_store.PathFor("core")));
        }

        [Test]
        public void Store_SavingAgain_KeepsTheFileItLoaded_ButDropsOlderSaves()
        {
            _store.Save(Plugin("core", Chunk("core", 0, HeightField.Flat(10f))));
            var store = Store();
            var loaded = store.Load("core");
            var loadedFile = TerrainFiles().Single();

            store.Save(Plugin("core", Chunk("core", 0, HeightField.Flat(20f))));
            var second = TerrainFiles().Except(new[] { loadedFile }).Single();
            store.Save(Plugin("core", Chunk("core", 0, HeightField.Flat(30f))));

            Assert.That(TerrainFiles(), Has.Length.EqualTo(2));
            Assert.That(TerrainFiles(), Does.Contain(loadedFile), "The loaded chunks still read from it.");
            Assert.That(TerrainFiles(), Does.Not.Contain(second));
            Assert.That(loaded.Records.OfType<TerrainChunkRecord>().Single().Heights.Load()[0, 0], Is.EqualTo(10f).Within(Precision));
            Assert.That(Store().Load("core").Records.OfType<TerrainChunkRecord>().Single().Heights.Load()[0, 0], Is.EqualTo(30f).Within(Precision));
        }

        [Test]
        public void Store_SavingLoadedTerrainAgain_CopiesItUnchanged_UnderTheSameName()
        {
            _store.Save(Plugin("core", Chunk("core", 0, Hills()), Chunk("core", 1, HeightField.Flat(5f))));
            var before = TerrainFiles().Single();
            var store = Store();
            var loaded = store.Load("core");

            store.Save(loaded);

            Assert.That(TerrainFiles(), Is.EqualTo(new[] { before }), "Same content, same file.");
        }

        [Test]
        public void Store_EditedChunk_IsSavedNextToCopiedOnes()
        {
            _store.Save(Plugin("core", Chunk("core", 0, Hills()), Chunk("core", 1, HeightField.Flat(5f))));
            var store = Store();
            var chunks = store.Load("core").Records.OfType<TerrainChunkRecord>().OrderBy(c => c.Coord.X).ToList();

            store.Save(Plugin("core", chunks[0], chunks[1].WithHeights(HeightField.Flat(42f))));
            var reloaded = Store().Load("core").Records.OfType<TerrainChunkRecord>().ToDictionary(c => c.Coord.X);

            AssertClose(reloaded[0].Heights.Load(), Hills());
            Assert.That(reloaded[1].Heights.Load()[3, 3], Is.EqualTo(42f).Within(Precision));
        }

        [Test]
        public void Store_LeavesOtherPluginsTerrainAlone()
        {
            _store.Save(Plugin("core", Chunk("core", 0, HeightField.Flat(1f))));
            _store.Save(new Plugin(new PluginManifest("core.extra", "1.0.0"), new[] { Chunk("core.extra", 0, HeightField.Flat(2f)) }));
            _store.Save(Plugin("core", Chunk("core", 0, HeightField.Flat(3f))));

            Assert.That(TerrainFiles().Count(f => PluginFileStore.IsTerrainFileOf("core.extra", f)), Is.EqualTo(1));
            Assert.That(TerrainFiles().Count(f => PluginFileStore.IsTerrainFileOf("core", f)), Is.EqualTo(1));
        }

        [TestCase("../elsewhere.0000000000000000.terrain")]
        [TestCase("other.0000000000000000.terrain")]
        [TestCase("core.0000000000000000.json")]
        public void Store_PluginNamingATerrainFileNotItsOwn_IsRejected(string name)
        {
            _store.Save(Plugin("core", Chunk("core", 0, HeightField.Flat(1f))));
            var real = TerrainFiles().Single();
            var path = _store.PathFor("core");
            File.Copy(Path.Combine(_directory, real), Path.Combine(_directory, Path.GetFileName(name)), overwrite: true);
            File.WriteAllText(path, File.ReadAllText(path).Replace(real, name));

            Assert.Throws<DataValidationException>(() => Store().Load("core"));
        }

        [Test]
        public void Store_MissingTerrainFile_IsReportedWithThePluginsName()
        {
            _store.Save(Plugin("core", Chunk("core", 0, HeightField.Flat(1f))));
            File.Delete(Path.Combine(_directory, TerrainFiles().Single()));

            var ex = Assert.Throws<DataValidationException>(() => Store().Load("core"));
            StringAssert.Contains("core.json", ex.Message);
            StringAssert.Contains("missing", ex.Message);
        }

        static HeightField HeightCodecRoundTrip(HeightField heights)
        {
            using (var stream = new MemoryStream())
            {
                TerrainFileWriter.Write(stream, new ChunkHeights[] { heights });
                var path = Path.Combine(Path.GetTempPath(), "forge-codec-" + Guid.NewGuid().ToString("N") + TerrainFile.Extension);
                try
                {
                    File.WriteAllBytes(path, stream.ToArray());
                    return TerrainFile.Open(path, new HeightCache()).Block(0, heights.Resolution).Load();
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }

        static long TerrainFileSize(HeightField heights)
        {
            using (var stream = new MemoryStream())
            {
                TerrainFileWriter.Write(stream, new ChunkHeights[] { heights });
                return stream.Length - TerrainFile.HeaderSize - TerrainFile.EntrySize;
            }
        }

        static string HashOf(HeightField heights)
        {
            using (var stream = new MemoryStream())
                return TerrainFileWriter.Write(stream, new ChunkHeights[] { heights }).Hash;
        }

        string WriteFile(HeightField heights)
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "t" + TerrainFile.Extension);
            using (var stream = File.Create(path))
                TerrainFileWriter.Write(stream, new ChunkHeights[] { heights });
            return path;
        }
    }
}
