using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Terrain;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class TerrainFollowerTests
    {
        const int Resolution = 17;
        static readonly RecordId Resting = RecordId.Parse("core:ref/resting");
        static readonly RecordId Floating = RecordId.Parse("core:ref/floating");
        static readonly RecordId Far = RecordId.Parse("core:ref/far");

        // One flat chunk at height 10 with an object resting on it at the brush center,
        // one floating 5 m above the ground nearby, and one beyond the brush.
        static EditSession NewSession() =>
            new EditSession(
                new[]
                {
                    Plugin("core",
                        new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", new ChunkCoord(0, 0)), new ChunkCoord(0, 0), HeightField.Flat(10f, Resolution)),
                        Base("core:rock"),
                        At("core:ref/resting", 20f, 10f, 20f),
                        At("core:ref/floating", 24f, 15f, 20f),
                        At("core:ref/far", 50f, 10f, 50f)),
                },
                Plugin("user", new[] { "core" }));

        static PlacedObjectRecord At(string id, float x, float y, float z) =>
            new PlacedObjectRecord(RecordId.Parse(id), RecordId.Parse("core:rock"), new Vector3(x, y, z), Quaternion.Identity, 1f);

        static SculptStroke RaiseStroke(EditSession session, int dabs)
        {
            var stroke = new SculptStroke(session.World.Records, new BrushSettings(BrushMode.Raise, 10f, 1f), new Vector2(20f, 20f));
            for (var i = 0; i < dabs; i++)
                stroke.ApplyDab(new Vector2(20f, 20f));
            return stroke;
        }

        static float HeightOf(EditSession session, RecordId id) => session.World.Records.Get<PlacedObjectRecord>(id).Position.Y;

        [Test]
        public void Objects_MoveByTheGroundChangeUnderThem_KeepingTheirOffset()
        {
            var session = NewSession();
            var stroke = RaiseStroke(session, dabs: 4);
            var follower = new TerrainFollower(session.World.Records);

            var followed = follower.Follow(stroke).ToDictionary(p => p.Id);

            var restingRise = stroke.HeightChangeAt(new Vector2(20f, 20f)).Value;
            var floatingRise = stroke.HeightChangeAt(new Vector2(24f, 20f)).Value;
            Assert.That(restingRise, Is.EqualTo(4 * BrushSettings.MaxDabHeight).Within(1e-4f));
            Assert.That(followed[Resting].Position.Y, Is.EqualTo(10f + restingRise).Within(1e-4f));
            Assert.That(followed[Floating].Position.Y, Is.EqualTo(15f + floatingRise).Within(1e-4f), "a floating object keeps its 5 m clearance");
            Assert.That(followed[Far].Position.Y, Is.EqualTo(10f), "outside the brush the ground didn't change");
            Assert.That(followed[Resting].Position.X, Is.EqualTo(20f));
        }

        [Test]
        public void Commit_WritesTerrainAndMovedObjectsAsOneUndoStep()
        {
            var session = NewSession();
            var stroke = RaiseStroke(session, dabs: 2);

            TerrainEdits.Commit(session, stroke, new TerrainFollower(session.World.Records));

            Assert.That(HeightOf(session, Resting), Is.EqualTo(11f).Within(1e-4f));
            Assert.That(session.World.Records.GetSource(Far), Is.EqualTo("core"), "unmoved objects are not overridden");
            Assert.That(session.UndoDescription, Is.EqualTo("Sculpt (Raise)"));

            session.Undo();

            Assert.That(HeightOf(session, Resting), Is.EqualTo(10f));
            Assert.That(session.World.Records.GetSource(Resting), Is.EqualTo("core"));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void Commit_WithoutFollower_LeavesObjectsAlone()
        {
            var session = NewSession();

            TerrainEdits.Commit(session, RaiseStroke(session, dabs: 2));

            Assert.That(HeightOf(session, Resting), Is.EqualTo(10f));
        }

        [Test]
        public void HeightChange_IsNullOffTheTerrain_AndZeroBeforeAnyDab()
        {
            var session = NewSession();
            var stroke = RaiseStroke(session, dabs: 0);

            Assert.That(stroke.HeightChangeAt(new Vector2(30f, 30f)), Is.EqualTo(0f));
            Assert.That(stroke.HeightChangeAt(new Vector2(-30f, 30f)), Is.Null);
            Assert.That(new TerrainFollower(session.World.Records).Follow(stroke), Is.Empty);
        }
    }
}
