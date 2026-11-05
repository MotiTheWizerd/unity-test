using System;
using System.IO;
using System.Linq;
using System.Numerics;
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
    /// <summary>Placed objects on disk: the block codec, the objects file, and plugins saved with their objects.</summary>
    public class ObjectStorageTests
    {
        string _directory;
        PluginFileStore _store;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "forge-objects-" + Guid.NewGuid().ToString("N"));
            _store = Store();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        PluginFileStore Store(ObjectCache cache = null) =>
            new PluginFileStore(_directory, new JsonPluginSerializer(RecordTypeRegistry.CreateDefault()), null, cache);

        static PlacedObjectRecord At(string id, float x, float z, string baseId = "core:rock", RecordId space = default) =>
            new PlacedObjectRecord(RecordId.Parse(id), RecordId.Parse(baseId), space.IsDefault ? Space : space,
                new Vector3(x, 12.5f, z), Quaternion.CreateFromYawPitchRoll(0.7f, 0.1f, 0f), 1.25f);

        static ObjectBlockKey Key(int x, int z, RecordId space = default) => new ObjectBlockKey(space.IsDefault ? Space : space, new ChunkCoord(x, z));

        string[] ObjectFiles() => Directory.GetFiles(_directory, "*" + ObjectsFile.Extension).Select(Path.GetFileName).OrderBy(n => n).ToArray();

        static PluginManifest Core(params string[] masters) => new PluginManifest("core", "1.0.0", masters);

        [Test]
        public void Codec_RoundTripsEveryField_ExactlyAndSorted()
        {
            var objects = new[] { At("core:ref/b", 3f, 4f), At("core:ref/a", 1f, 2f, "core:tree") }.OrderBy(o => o.Id).ToArray();

            var copy = ObjectCodec.Decode(ObjectCodec.Encode(objects), Key(0, 0), Core());

            Assert.That(copy.Select(o => o.Id), Is.EqualTo(objects.Select(o => o.Id)));
            for (var i = 0; i < objects.Length; i++)
            {
                Assert.That(copy[i].BaseId, Is.EqualTo(objects[i].BaseId));
                Assert.That(copy[i].Space, Is.EqualTo(Space));
                Assert.That(copy[i].Position, Is.EqualTo(objects[i].Position));
                Assert.That(copy[i].Rotation, Is.EqualTo(objects[i].Rotation));
                Assert.That(copy[i].Scale, Is.EqualTo(objects[i].Scale));
            }
        }

        [Test]
        public void Codec_RejectsObjectsOfAnotherChunk_ForeignNamespaces_AndDamage()
        {
            var block = ObjectCodec.Encode(new[] { At("core:ref/a", 1f, 2f) });

            Assert.Throws<DataValidationException>(() => ObjectCodec.Decode(block, Key(1, 0), Core()), "stands in chunk (0, 0)");
            Assert.Throws<DataValidationException>(() => ObjectCodec.Decode(ObjectCodec.Encode(new[] { At("other:ref/a", 1f, 2f) }), Key(0, 0), Core()), "no such master");
            Assert.DoesNotThrow(() => ObjectCodec.Decode(ObjectCodec.Encode(new[] { At("other:ref/a", 1f, 2f) }), Key(0, 0), Core("other")));
            Assert.Throws<DataValidationException>(() => ObjectCodec.Decode(block.Take(block.Length - 5).ToArray(), Key(0, 0), Core()), "cut short");
            Assert.Throws<DataValidationException>(() => ObjectCodec.Decode(block.Concat(new byte[] { 1 }).ToArray(), Key(0, 0), Core()), "trailing data");
        }

        [Test]
        public void File_OpensByReadingTheTableOnly_ThenReadsABlockWhenAsked()
        {
            var blocks = new InMemoryObjectBlocks("core", new[] { At("core:ref/a", 1f, 1f), At("core:ref/b", 70f, 1f), At("core:ref/c", 2f, 2f, space: OtherSpace), At("other:ref/x", 3f, 3f) });
            var path = WriteFile(blocks);
            var cache = new ObjectCache();

            var file = ObjectsFile.Open(path, Core("other"), cache);

            Assert.That(file.Keys, Is.EqualTo(new[] { Key(0, 0), Key(1, 0), Key(0, 0, OtherSpace) }));
            Assert.That((file.TotalCount, file.CountOf(Key(0, 0)), file.CountOf(Key(5, 5))), Is.EqualTo((4, 2, 0)));
            Assert.That(file.Overrides.Select(id => id.ToString()), Is.EqualTo(new[] { "other:ref/x" }));
            Assert.That(file.TryLocate(RecordId.Parse("core:ref/b"), out var key) && key == Key(1, 0), Is.True);
            Assert.That(file.TryLocate(RecordId.Parse("core:ref/none"), out _), Is.False);
            Assert.That(cache.Count, Is.EqualTo(0), "Nothing decoded yet.");

            var read = file.Read(Key(0, 0));
            Assert.That(read.Select(o => o.Id.ToString()), Is.EqualTo(new[] { "core:ref/a", "other:ref/x" }));
            Assert.That(read[0].Position.X, Is.EqualTo(1f));
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(file.Read(Key(0, 0)), Is.SameAs(read), "Read once, kept.");
            Assert.That(file.Read(Key(9, 9)), Is.Empty);
        }

        [Test]
        public void File_SameContent_SameHash()
        {
            var a = new InMemoryObjectBlocks("core", new[] { At("core:ref/a", 1f, 1f) });
            var b = new InMemoryObjectBlocks("core", new[] { At("core:ref/a", 1f, 1f) });
            var c = new InMemoryObjectBlocks("core", new[] { At("core:ref/a", 2f, 1f) });

            Assert.That(HashOf(a), Is.EqualTo(HashOf(b)));
            Assert.That(HashOf(a), Is.Not.EqualTo(HashOf(c)));
            Assert.That(HashOf(a), Has.Length.EqualTo(WrittenObjects.HashLength));
        }

        [Test]
        public void File_NotObjects_CutShort_Missing_OrWithADamagedBlock_AreRejected()
        {
            Directory.CreateDirectory(_directory);
            var bad = Path.Combine(_directory, "bad.objects");
            File.WriteAllText(bad, "not an objects file");
            Assert.Throws<DataValidationException>(() => ObjectsFile.Open(bad, Core(), new ObjectCache()));
            Assert.Throws<DataValidationException>(() => ObjectsFile.Open(Path.Combine(_directory, "gone.objects"), Core(), new ObjectCache()));

            var path = WriteFile(new InMemoryObjectBlocks("core", new[] { At("core:ref/a", 1f, 1f) }));
            var bytes = File.ReadAllBytes(path);
            File.WriteAllBytes(path, bytes.Take(bytes.Length - 3).ToArray());
            Assert.Throws<DataValidationException>(() => ObjectsFile.Open(path, Core(), new ObjectCache()));

            File.WriteAllBytes(path, bytes);
            for (var i = ObjectsFile.HeaderSize; i < ObjectsFile.HeaderSize + 12; i++)
                bytes[i] ^= 0x5A;
            File.WriteAllBytes(path, bytes);
            var file = ObjectsFile.Open(path, Core(), new ObjectCache());
            Assert.Throws<DataValidationException>(() => file.Read(Key(0, 0)));
        }

        [Test]
        public void Store_SavesObjectsBesideThePlugin_AndLoadsThemOnDemand()
        {
            _store.Save(Plugin("core", Base("core:rock"), At("core:ref/a", 1f, 1f), At("core:ref/b", 70f, 1f)));

            var cache = new ObjectCache();
            var loaded = Store(cache).Load("core");

            var file = ObjectFiles().Single();
            Assert.That(PluginFileStore.IsObjectsFileOf("core", file), Is.True);
            StringAssert.Contains($"\"objects\": \"{file}\"", File.ReadAllText(_store.PathFor("core")));
            StringAssert.DoesNotContain("placed_object", File.ReadAllText(_store.PathFor("core")));
            Assert.That(loaded.Records.Select(r => r.Id.ToString()), Is.EqualTo(new[] { "core:rock" }));
            Assert.That(loaded.Objects.TotalCount, Is.EqualTo(2));
            Assert.That(cache.Count, Is.EqualTo(0), "Loading a plugin reads no objects.");
            Assert.That(loaded.Objects.Read(Key(1, 0)).Single().Id.ToString(), Is.EqualTo("core:ref/b"));
            Assert.That(cache.Count, Is.EqualTo(1));
        }

        [Test]
        public void Store_PluginWithoutObjects_HasNoObjectsFile()
        {
            _store.Save(Plugin("core", Base("core:rock")));

            Assert.That(ObjectFiles(), Is.Empty);
            StringAssert.DoesNotContain("\"objects\"", File.ReadAllText(_store.PathFor("core")));
        }

        [Test]
        public void Store_SavingLoadedObjectsAgain_CopiesThemUnchanged_UnderTheSameName()
        {
            _store.Save(Plugin("core", Base("core:rock"), At("core:ref/a", 1f, 1f), At("core:ref/b", 70f, 1f)));
            var before = ObjectFiles().Single();
            var store = Store();
            var loaded = store.Load("core");

            store.Save(loaded);

            Assert.That(ObjectFiles(), Is.EqualTo(new[] { before }), "Same content, same file.");
        }

        [Test]
        public void Store_SavingAgain_KeepsTheFileItLoaded_ButDropsOlderSaves()
        {
            _store.Save(Plugin("core", At("core:ref/a", 1f, 1f)));
            var store = Store();
            var loaded = store.Load("core");
            var loadedFile = ObjectFiles().Single();

            store.Save(Plugin("core", At("core:ref/a", 2f, 1f)));
            var second = ObjectFiles().Except(new[] { loadedFile }).Single();
            store.Save(Plugin("core", At("core:ref/a", 3f, 1f)));

            Assert.That(ObjectFiles(), Has.Length.EqualTo(2));
            Assert.That(ObjectFiles(), Does.Contain(loadedFile), "The loaded blocks still read from it.");
            Assert.That(ObjectFiles(), Does.Not.Contain(second));
            Assert.That(loaded.Objects.Read(Key(0, 0)).Single().Position.X, Is.EqualTo(1f));
            Assert.That(Store().Load("core").Objects.Read(Key(0, 0)).Single().Position.X, Is.EqualTo(3f));
        }

        [Test]
        public void Store_AMastersObjectsStream_AndAModsOverridesResolveOverThem()
        {
            _store.Save(Plugin("core", Base("core:rock"), At("core:ref/a", 1f, 1f), At("core:ref/b", 70f, 1f)));
            _store.Save(new Plugin(new PluginManifest("mod", "1.0.0", new[] { "core" }), new Record[] { At("core:ref/a", 100f, 1f) }));
            var cache = new ObjectCache();
            var store = Store(cache);

            var world = LoadOrderResolver.Resolve(new[] { store.Load("core"), store.Load("mod") });

            Assert.That(cache.Count, Is.EqualTo(0), "Resolving reads no objects.");
            Assert.That(world.Records.Count, Is.EqualTo(3));
            Assert.That(world.Records.ObjectsIn(Space, new ChunkCoord(0, 0)), Is.Empty, "a moved east by the mod");
            Assert.That(world.Records.ObjectsIn(Space, new ChunkCoord(1, 0)).Select(o => (o.Id.Name, o.Position.X)), Is.EqualTo(new[] { ("ref/a", 100f), ("ref/b", 70f) }));
            Assert.That(world.Records.GetSource(RecordId.Parse("core:ref/a")), Is.EqualTo("mod"));
        }

        [TestCase("../elsewhere.0000000000000000.objects")]
        [TestCase("other.0000000000000000.objects")]
        public void Store_PluginNamingAnObjectsFileNotItsOwn_IsRejected(string name)
        {
            _store.Save(Plugin("core", At("core:ref/a", 1f, 1f)));
            var real = ObjectFiles().Single();
            var path = _store.PathFor("core");
            File.Copy(Path.Combine(_directory, real), Path.Combine(_directory, Path.GetFileName(name)), overwrite: true);
            File.WriteAllText(path, File.ReadAllText(path).Replace(real, name));

            Assert.Throws<DataValidationException>(() => Store().Load("core"));
        }

        [Test]
        public void Store_MissingObjectsFile_IsReportedWithThePluginsName()
        {
            _store.Save(Plugin("core", At("core:ref/a", 1f, 1f)));
            File.Delete(Path.Combine(_directory, ObjectFiles().Single()));

            var ex = Assert.Throws<DataValidationException>(() => Store().Load("core"));
            StringAssert.Contains("core.json", ex.Message);
            StringAssert.Contains("missing", ex.Message);
        }

        [Test]
        public void Cache_LetsGoOfTheLeastRecentlyUsedBlocks_OverBudget()
        {
            var cache = new ObjectCache(ObjectCache.BytesPerObject * 2);
            var loads = 0;
            System.Collections.Generic.IReadOnlyList<PlacedObjectRecord> Load() { loads++; return new[] { At("core:ref/a", 1f, 1f) }; }
            object a = new object(), b = new object(), c = new object();

            cache.GetOrLoad(a, Load);
            cache.GetOrLoad(b, Load);
            cache.GetOrLoad(a, Load);
            cache.GetOrLoad(c, Load);

            Assert.That((loads, cache.Count), Is.EqualTo((3, 2)));
            cache.GetOrLoad(a, Load);
            Assert.That(loads, Is.EqualTo(3), "a was used recently, so b went.");
        }

        string WriteFile(IObjectBlocks blocks)
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, "o" + ObjectsFile.Extension);
            using (var stream = File.Create(path))
                ObjectsFileWriter.Write(stream, blocks);
            return path;
        }

        static string HashOf(IObjectBlocks blocks)
        {
            using (var stream = new MemoryStream())
                return ObjectsFileWriter.Write(stream, blocks).Hash;
        }
    }
}
