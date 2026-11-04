using System;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class SculptStrokeTests
    {
        const int Resolution = 17;
        const float Spacing = WorldGrid.ChunkSize / (Resolution - 1); // 4 m

        static TerrainChunkRecord Chunk(int x, int z, float height = 10f) => Chunk(Space, x, z, height);

        static TerrainChunkRecord Chunk(RecordId space, int x, int z, float height = 10f, int resolution = Resolution) =>
            new TerrainChunkRecord(
                TerrainChunkRecord.IdFor("core", space, new ChunkCoord(x, z)),
                space,
                new ChunkCoord(x, z),
                HeightField.Flat(height, resolution));

        // Four chunks meeting at the world origin.
        static EditSession NewSession(params TerrainChunkRecord[] extra) =>
            new EditSession(
                new[] { Plugin("core", new Record[] { Chunk(-1, -1), Chunk(0, -1), Chunk(-1, 0), Chunk(0, 0) }.Concat(extra).ToArray()) },
                Plugin("user", new[] { "core" }));

        static SculptStroke Stroke(EditSession session, BrushMode mode, float radius = 10f, float strength = 1f, Vector2 start = default) =>
            new SculptStroke(session.World.Records, Space, new BrushSettings(mode, radius, strength), start);

        [Test]
        public void BrushWeight_IsOneAtCenterAndZeroAtRim()
        {
            var brush = new BrushSettings(BrushMode.Raise, 10f, 1f);

            Assert.That(brush.Weight(0f), Is.EqualTo(1f));
            Assert.That(brush.Weight(5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(brush.Weight(10f), Is.EqualTo(0f));
            Assert.That(brush.Weight(50f), Is.EqualTo(0f));
        }

        [Test]
        public void BrushSettings_RejectOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BrushSettings(BrushMode.Raise, 0.5f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BrushSettings(BrushMode.Raise, 10f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BrushSettings(BrushMode.Raise, 10f, 1.5f));
        }

        [Test]
        public void Raise_LiftsCenterMostAndLeavesOutsideUntouched()
        {
            var stroke = Stroke(NewSession(), BrushMode.Raise);
            var center = new Vector2(20f, 20f);

            stroke.ApplyDab(center);

            Assert.That(stroke.SampleHeight(center), Is.EqualTo(10f + BrushSettings.MaxDabHeight).Within(1e-4f));
            Assert.That(stroke.SampleHeight(new Vector2(20f + 8f, 20f)), Is.GreaterThan(10f).And.LessThan(10.5f));
            Assert.That(stroke.SampleHeight(new Vector2(20f + 12f, 20f)), Is.EqualTo(10f));
            Assert.That(stroke.TouchedChunks, Is.EquivalentTo(new[] { new ChunkCoord(0, 0) }));
        }

        [Test]
        public void DabOnChunkCorner_KeepsSharedSamplesIdentical()
        {
            var stroke = Stroke(NewSession(), BrushMode.Raise, radius: 12f);

            var changed = stroke.ApplyDab(Vector2.Zero);

            Assert.That(changed.Count, Is.EqualTo(4));
            // The corner sample at the origin exists in all four chunks.
            var corner = new[]
            {
                stroke.HeightsOf(new ChunkCoord(0, 0))[0, 0],
                stroke.HeightsOf(new ChunkCoord(-1, 0))[Resolution - 1, 0],
                stroke.HeightsOf(new ChunkCoord(0, -1))[0, Resolution - 1],
                stroke.HeightsOf(new ChunkCoord(-1, -1))[Resolution - 1, Resolution - 1],
            };
            Assert.That(corner.Distinct().Count(), Is.EqualTo(1));
            Assert.That(corner[0], Is.EqualTo(10.5f).Within(1e-4f));

            // Every shared edge sample along x = 0 matches between the two chunks.
            for (var z = 0; z < Resolution; z++)
                Assert.That(stroke.HeightsOf(new ChunkCoord(0, 0))[0, z], Is.EqualTo(stroke.HeightsOf(new ChunkCoord(-1, 0))[Resolution - 1, z]));
        }

        [Test]
        public void Lower_ClampsAtZero()
        {
            var stroke = Stroke(NewSession(), BrushMode.Lower);

            for (var i = 0; i < 100; i++)
                stroke.ApplyDab(new Vector2(20f, 20f));

            Assert.That(stroke.SampleHeight(new Vector2(20f, 20f)), Is.EqualTo(0f));
        }

        [Test]
        public void Flatten_ConvergesToHeightAtStrokeStart()
        {
            var session = NewSession();
            // Raise a hill first, then flatten it toward the surrounding height.
            var hill = Stroke(session, BrushMode.Raise);
            for (var i = 0; i < 10; i++)
                hill.ApplyDab(new Vector2(20f, 20f));
            TerrainEdits.Commit(session, hill);

            var flatten = Stroke(session, BrushMode.Flatten, radius: 20f, start: new Vector2(48f, 48f));
            for (var i = 0; i < 50; i++)
                flatten.ApplyDab(new Vector2(20f, 20f));

            Assert.That(flatten.FlattenHeight, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(flatten.SampleHeight(new Vector2(20f, 20f)), Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void Smooth_ReducesASpike()
        {
            var session = NewSession();
            var spike = Stroke(session, BrushMode.Raise, radius: 4f);
            for (var i = 0; i < 10; i++)
                spike.ApplyDab(new Vector2(20f, 20f));
            TerrainEdits.Commit(session, spike);
            var peak = Stroke(session, BrushMode.Smooth).SampleHeight(new Vector2(20f, 20f)).Value;

            var smooth = Stroke(session, BrushMode.Smooth);
            smooth.ApplyDab(new Vector2(20f, 20f));

            Assert.That(smooth.SampleHeight(new Vector2(20f, 20f)), Is.LessThan(peak).And.GreaterThan(10f));
        }

        [Test]
        public void DabOutsideTerrain_ChangesNothing()
        {
            var stroke = Stroke(NewSession(), BrushMode.Raise);

            Assert.That(stroke.ApplyDab(new Vector2(500f, 500f)), Is.Empty);
            Assert.That(stroke.SampleHeight(new Vector2(500f, 500f)), Is.Null);
            Assert.That(stroke.ChangedRecords(), Is.Empty);
        }

        [Test]
        public void StrokeDoesNotTouchTheWorldUntilCommitted()
        {
            var session = NewSession();
            var stroke = Stroke(session, BrushMode.Raise);

            stroke.ApplyDab(new Vector2(20f, 20f));

            Assert.That(session.CanUndo, Is.False);
            var chunk = session.World.Records.Get<TerrainChunkRecord>(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(0, 0)));
            Assert.That(chunk.Heights[5, 5], Is.EqualTo(10f));
        }

        [Test]
        public void Commit_WritesChangedChunksAsOneUndoableOverride()
        {
            var session = NewSession();
            var stroke = Stroke(session, BrushMode.Raise, radius: 12f);
            stroke.ApplyDab(Vector2.Zero);
            stroke.ApplyDab(new Vector2(4f, 4f));

            var written = TerrainEdits.Commit(session, stroke);

            Assert.That(written, Is.EqualTo(4));
            Assert.That(session.UndoDescription, Is.EqualTo("Sculpt (Raise)"));
            var id = TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(0, 0));
            Assert.That(session.World.Records.GetSource(id), Is.EqualTo("user"));
            Assert.That(session.World.Records.Get<TerrainChunkRecord>(id).Heights[0, 0], Is.GreaterThan(10f));

            session.Undo();
            Assert.That(session.World.Records.Get<TerrainChunkRecord>(id).Heights[0, 0], Is.EqualTo(10f));
            Assert.That(session.World.Records.GetSource(id), Is.EqualTo("core"));
        }

        [Test]
        public void Commit_WithoutChanges_RecordsNothing()
        {
            var session = NewSession();

            Assert.That(TerrainEdits.Commit(session, Stroke(session, BrushMode.Raise)), Is.EqualTo(0));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void MixedResolutions_AreRejected()
        {
            var odd = Chunk(Space, 5, 5, 0f, resolution: 33);

            Assert.Throws<InvalidOperationException>(() => Stroke(NewSession(odd), BrushMode.Raise));
        }

        [Test]
        public void AnotherSpace_IsNeitherReadNorWritten()
        {
            // The other space has land at the same coordinates, higher and at another resolution.
            var session = NewSession(Chunk(OtherSpace, 0, 0, 50f, resolution: 33), Chunk(OtherSpace, 9, 9, 50f, resolution: 33));
            var stroke = Stroke(session, BrushMode.Raise, radius: 12f);

            Assert.That(stroke.SampleHeight(new Vector2(20f, 20f)), Is.EqualTo(10f));
            Assert.That(stroke.SampleHeight(new Vector2(9 * 64f + 10f, 9 * 64f + 10f)), Is.Null);

            stroke.ApplyDab(new Vector2(20f, 20f));
            TerrainEdits.Commit(session, stroke);

            var other = TerrainChunkRecord.IdFor("core", OtherSpace, new ChunkCoord(0, 0));
            Assert.That(session.World.Records.GetSource(other), Is.EqualTo("core"), "the other space's chunk isn't overridden");
            Assert.That(session.World.Records.Get<TerrainChunkRecord>(other).Heights[5, 5], Is.EqualTo(50f));
            Assert.That(session.World.Records.GetSource(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(0, 0))), Is.EqualTo("user"));
        }
    }
}
