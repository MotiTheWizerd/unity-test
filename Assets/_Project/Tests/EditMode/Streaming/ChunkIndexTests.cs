using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Streaming;
using Forge.Tests.Data;
using NUnit.Framework;

namespace Forge.Tests.Streaming
{
    public class ChunkIndexTests
    {
        [Test]
        public void GroupsObjectsByTheChunkUnderTheirPosition()
        {
            var index = Build(Store(TestRecords.Plugin("core",
                TestRecords.Base("core:rock"),
                PlacedAt("core:ref/a", 10f, 10f),
                PlacedAt("core:ref/b", 70f, 10f),
                PlacedAt("core:ref/c", -5f, 130f),
                PlacedAt("core:ref/d", 63f, 1f))));

            Assert.That(Names(index.ObjectsIn(new ChunkCoord(0, 0))), Is.EqualTo(new[] { "ref/a", "ref/d" }));
            Assert.That(Names(index.ObjectsIn(new ChunkCoord(1, 0))), Is.EqualTo(new[] { "ref/b" }));
            Assert.That(Names(index.ObjectsIn(new ChunkCoord(-1, 2))), Is.EqualTo(new[] { "ref/c" }));
            Assert.That(index.ObjectsIn(new ChunkCoord(5, 5)), Is.Empty);
            Assert.That(index.ObjectCount, Is.EqualTo(4));
        }

        [Test]
        public void IndexesTerrainByCoordinate()
        {
            var index = Build(Store(TestRecords.Plugin("core", Chunk("core", 0, 0), Chunk("core", -1, 3))));

            Assert.That(index.TerrainCount, Is.EqualTo(2));
            Assert.That(index.TryGetTerrain(new ChunkCoord(-1, 3), out var chunk), Is.True);
            Assert.That(chunk.Id.ToString(), Is.EqualTo("core:terrain/core/space/main/c-1_3"));
            Assert.That(index.TryGetTerrain(new ChunkCoord(1, 1), out _), Is.False);
        }

        [Test]
        public void DuplicateCoordinate_LowestIdWins_OthersAreShadowed()
        {
            var core = TestRecords.Plugin("core", Chunk("core", 0, 0));
            var mod = TestRecords.Plugin("amod", new[] { "core" }, Chunk("amod", 0, 0));
            var world = LoadOrderResolver.Resolve(new[] { core, mod });

            var index = Build(world.Records);

            Assert.That(index.TryGetTerrain(new ChunkCoord(0, 0), out var winner), Is.True);
            Assert.That(winner.Id.Namespace, Is.EqualTo("amod"));
            Assert.That(index.ShadowedTerrain.Select(c => c.Id.ToString()), Is.EqualTo(new[] { "core:terrain/core/space/main/c0_0" }));
        }

        [Test]
        public void EmptyIndex_HasNothing()
        {
            var index = new ChunkIndex();

            Assert.That(index.TerrainCount, Is.Zero);
            Assert.That(index.ObjectsIn(new ChunkCoord(0, 0)), Is.Empty);
        }

        [Test]
        public void Build_IndexesOnlyTheRecordsOfItsSpace()
        {
            var index = Build(Store(TestRecords.Plugin("core",
                TestRecords.Base("core:rock"),
                Chunk("core", 0, 0),
                Chunk("core", 0, 0, TestRecords.OtherSpace),
                Chunk("core", 1, 0, TestRecords.OtherSpace),
                PlacedAt("core:ref/here", 10f, 10f),
                PlacedAt("core:ref/there", 10f, 10f, TestRecords.OtherSpace))));

            Assert.That(index.Space, Is.EqualTo(TestRecords.Space));
            Assert.That(index.TerrainCount, Is.EqualTo(1));
            Assert.That(index.TryGetTerrain(new ChunkCoord(0, 0), out var chunk) && chunk.Space == TestRecords.Space, Is.True);
            Assert.That(index.ShadowedTerrain, Is.Empty, "a chunk at the same coordinate in another space doesn't compete");
            Assert.That(index.TryGetTerrain(new ChunkCoord(1, 0), out _), Is.False);
            Assert.That(Names(index.ObjectsIn(new ChunkCoord(0, 0))), Is.EqualTo(new[] { "ref/here" }));
            Assert.That(index.ObjectCount, Is.EqualTo(1));
            Assert.That(index.TryLocate(RecordId.Parse("core:ref/there"), out _), Is.False);
        }

        [Test]
        public void Update_IgnoresRecordsOfAnotherSpace()
        {
            var before = TestRecords.Plugin("core", TestRecords.Base("core:rock"), PlacedAt("core:ref/a", 10f, 10f));
            var after = TestRecords.Plugin("core", TestRecords.Base("core:rock"), PlacedAt("core:ref/a", 10f, 10f),
                PlacedAt("core:ref/there", 20f, 10f, TestRecords.OtherSpace), Chunk("core", 2, 2, TestRecords.OtherSpace));
            var index = Build(Store(before));

            var dirty = index.Update(Store(after), new[]
            {
                RecordId.Parse("core:ref/there"),
                TerrainChunkRecord.IdFor("core", TestRecords.OtherSpace, new ChunkCoord(2, 2)),
            });

            Assert.That(dirty, Is.Empty);
            Assert.That(index.ObjectCount, Is.EqualTo(1));
            Assert.That(index.TerrainCount, Is.Zero);
            Assert.That(Names(index.ObjectsIn(new ChunkCoord(0, 0))), Is.EqualTo(new[] { "ref/a" }));
        }

        [Test]
        public void Update_AnObjectMovedToAnotherSpace_LeavesTheIndex()
        {
            var before = TestRecords.Plugin("core", TestRecords.Base("core:rock"), PlacedAt("core:ref/a", 10f, 10f));
            var after = TestRecords.Plugin("core", TestRecords.Base("core:rock"), PlacedAt("core:ref/a", 10f, 10f, TestRecords.OtherSpace));
            var index = Build(Store(before));

            var dirty = index.Update(Store(after), new[] { RecordId.Parse("core:ref/a") });

            Assert.That(dirty, Is.EquivalentTo(new[] { new ChunkCoord(0, 0) }));
            Assert.That(index.ObjectCount, Is.Zero);
            Assert.That(index.ObjectsIn(new ChunkCoord(0, 0)), Is.Empty);
        }

        [Test]
        public void Update_MovesObjectBetweenChunks_AndReportsBothAsDirty()
        {
            var before = TestRecords.Plugin("core", TestRecords.Base("core:rock"), PlacedAt("core:ref/a", 10f, 10f), PlacedAt("core:ref/b", 20f, 10f));
            var after = TestRecords.Plugin("core", TestRecords.Base("core:rock"), PlacedAt("core:ref/a", 100f, 10f), PlacedAt("core:ref/b", 20f, 10f));
            var index = Build(Store(before));

            var dirty = index.Update(Store(after), new[] { RecordId.Parse("core:ref/a") });

            Assert.That(dirty, Is.EquivalentTo(new[] { new ChunkCoord(0, 0), new ChunkCoord(1, 0) }));
            Assert.That(Names(index.ObjectsIn(new ChunkCoord(0, 0))), Is.EqualTo(new[] { "ref/b" }));
            Assert.That(Names(index.ObjectsIn(new ChunkCoord(1, 0))), Is.EqualTo(new[] { "ref/a" }));
            Assert.That(index.ObjectCount, Is.EqualTo(2));
        }

        [Test]
        public void Update_TerrainWinnerChanges_WhenALowerIdAppearsOrGoes()
        {
            var core = TestRecords.Plugin("core", Chunk("core", 0, 0));
            var withMod = LoadOrderResolver.Resolve(new[] { core, TestRecords.Plugin("amod", new[] { "core" }, Chunk("amod", 0, 0)) });
            var index = Build(Store(core));
            var modChunk = TerrainChunkRecord.IdFor("amod", TestRecords.Space, new ChunkCoord(0, 0));

            index.Update(withMod.Records, new[] { modChunk });

            Assert.That(index.TryGetTerrain(new ChunkCoord(0, 0), out var winner) && winner.Id == modChunk, Is.True);
            Assert.That(index.TryLocate(RecordId.Parse("core:terrain/core/space/main/c0_0"), out _), Is.False, "shadowed terrain is not shown");
            Assert.That(index.TryLocate(modChunk, out _), Is.True);

            index.Update(Store(core), new[] { modChunk });

            Assert.That(index.TryGetTerrain(new ChunkCoord(0, 0), out winner) && winner.Id.Namespace == "core", Is.True);
            Assert.That(index.ShadowedAt(new ChunkCoord(0, 0)), Is.Empty);
        }

        [Test]
        public void RandomMovesAddsAndRemoves_MatchAFreshBuild()
        {
            var random = new System.Random(7);
            var model = new Dictionary<int, PlacedObjectRecord>();
            for (var i = 0; i < 40; i++)
                model[i] = RandomPlaced(i, random);
            var index = Build(StoreOf(model.Values));

            for (var step = 0; step < 300; step++)
            {
                var i = random.Next(40);
                if (random.Next(4) == 0 && model.ContainsKey(i))
                    model.Remove(i);
                else
                    model[i] = RandomPlaced(i, random);
                var store = StoreOf(model.Values);

                index.Update(store, new[] { IdOf(i) });

                var fresh = Build(store);
                Assert.That(index.ObjectCount, Is.EqualTo(fresh.ObjectCount), $"step {step}");
                for (var x = -3; x <= 3; x++)
                for (var z = -3; z <= 3; z++)
                {
                    var coord = new ChunkCoord(x, z);
                    Assert.That(index.ObjectsIn(coord), Is.EqualTo(fresh.ObjectsIn(coord)), $"step {step}, chunk {coord}");
                }
            }
        }

        static RecordId IdOf(int i) => RecordId.Parse($"core:ref/o{i:D2}");

        // Positions within chunks -3..2 on both axes.
        static PlacedObjectRecord RandomPlaced(int i, System.Random random) =>
            PlacedAt(IdOf(i).ToString(), (float)(random.NextDouble() * 384 - 192), (float)(random.NextDouble() * 384 - 192));

        static IRecordStore StoreOf(IEnumerable<PlacedObjectRecord> placed) =>
            Store(TestRecords.Plugin("core", placed.Cast<Record>().Prepend(TestRecords.Base("core:rock")).ToArray()));

        static IRecordStore Store(Plugin plugin) => LoadOrderResolver.Resolve(new[] { plugin }).Records;

        static ChunkIndex Build(IRecordStore store) => ChunkIndex.Build(store, TestRecords.Space);

        static PlacedObjectRecord PlacedAt(string id, float x, float z, RecordId space = default) =>
            new PlacedObjectRecord(RecordId.Parse(id), RecordId.Parse("core:rock"), space.IsDefault ? TestRecords.Space : space,
                new Vector3(x, 0f, z), Quaternion.Identity, 1f);

        static TerrainChunkRecord Chunk(string ns, int x, int z, RecordId space = default)
        {
            var coord = new ChunkCoord(x, z);
            if (space.IsDefault)
                space = TestRecords.Space;
            return new TerrainChunkRecord(TerrainChunkRecord.IdFor(ns, space, coord), space, coord, HeightField.Flat(0f, 17));
        }

        static string[] Names(System.Collections.Generic.IEnumerable<PlacedObjectRecord> records) =>
            records.Select(r => r.Id.Name).ToArray();
    }
}
