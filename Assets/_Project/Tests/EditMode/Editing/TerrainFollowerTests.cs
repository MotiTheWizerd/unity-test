using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Data.Terrain;
using Forge.Editing;
using Forge.Editing.Placement;
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
        static EditSession NewSession(params Record[] extra) =>
            new EditSession(
                new[]
                {
                    Plugin("core", new Record[]
                    {
                        new TerrainChunkRecord(TerrainChunkRecord.IdFor("core", Space, new ChunkCoord(0, 0)), Space, new ChunkCoord(0, 0), HeightField.Flat(10f, Resolution)),
                        Base("core:rock"),
                        At("core:ref/resting", 20f, 10f, 20f),
                        At("core:ref/floating", 24f, 15f, 20f),
                        At("core:ref/far", 50f, 10f, 50f),
                    }.Concat(extra).ToArray()),
                },
                Plugin("user", new[] { "core" }));

        static PlacedObjectRecord At(string id, float x, float y, float z) => At(Space, id, x, y, z);

        static PlacedObjectRecord At(RecordId space, string id, float x, float y, float z) =>
            new PlacedObjectRecord(RecordId.Parse(id), RecordId.Parse("core:rock"), space, new Vector3(x, y, z), Quaternion.Identity, 1f);

        static SculptStroke RaiseStroke(EditSession session, int dabs)
        {
            var stroke = new SculptStroke(session.World.Records, Space, new BrushSettings(BrushMode.Raise, 10f, 1f), new Vector2(20f, 20f));
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
            var follower = new TerrainFollower(session.World.Records, Space);

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

            TerrainEdits.Commit(session, stroke, new TerrainFollower(session.World.Records, Space));

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
        public void SlopeObjects_AreRetiltedToTheNewGround_KeepingTheirClearance()
        {
            var session = NewSession();
            // A hill rising east of the resting "fence" (3 m long along X): its east end ends up higher.
            var stroke = new SculptStroke(session.World.Records, Space, new BrushSettings(BrushMode.Raise, 8f, 1f), new Vector2(26f, 20f));
            for (var i = 0; i < 4; i++)
                stroke.ApplyDab(new Vector2(26f, 20f));
            var fence = new PlacementShape(new Footprint(3f, 0.2f), 0f);
            var follower = new TerrainFollower(session.World.Records, Space, placed => placed.Id == Resting || placed.Id == Floating ? fence : (PlacementShape?)null);

            var followed = follower.Follow(stroke).ToDictionary(p => p.Id);

            var resting = followed[Resting];
            var up = Vector3.Transform(Vector3.UnitY, resting.Rotation);
            Assert.That(up.X, Is.LessThan(-0.05f), "Tilted away from the raised east end.");
            var eastEnd = resting.Position + Vector3.Transform(new Vector3(1.5f, 0f, 0f), resting.Rotation);
            var westEnd = resting.Position + Vector3.Transform(new Vector3(-1.5f, 0f, 0f), resting.Rotation);
            // The ground is curved (brush falloff), so a straight fence meets it only approximately.
            Assert.That(eastEnd.Y, Is.EqualTo(stroke.SampleHeight(new Vector2(eastEnd.X, eastEnd.Z)).Value).Within(0.06f));
            Assert.That(westEnd.Y, Is.EqualTo(stroke.SampleHeight(new Vector2(westEnd.X, westEnd.Z)).Value).Within(0.06f));

            var floating = followed[Floating];
            var groundUnder = stroke.SampleHeight(new Vector2(24f, 20f)).Value;
            Assert.That(floating.Position.Y - groundUnder, Is.EqualTo(5f).Within(0.5f), "Keeps (about) its 5 m clearance.");
        }

        [Test]
        public void SlopeObjects_OnUnchangedGround_AreLeftExactlyAsTheyWere()
        {
            var session = NewSession();
            var stroke = new SculptStroke(session.World.Records, Space, new BrushSettings(BrushMode.Raise, 3f, 1f), new Vector2(5f, 5f));
            stroke.ApplyDab(new Vector2(5f, 5f));
            var shape = new PlacementShape(new Footprint(1f, 1f), 0f);
            var original = session.World.Records.Get<PlacedObjectRecord>(Resting);

            var followed = new TerrainFollower(session.World.Records, Space, _ => shape).Follow(stroke).ToDictionary(p => p.Id);

            Assert.That(followed[Resting], Is.SameAs(original));
        }

        [Test]
        public void HeightChange_IsNullOffTheTerrain_AndZeroBeforeAnyDab()
        {
            var session = NewSession();
            var stroke = RaiseStroke(session, dabs: 0);

            Assert.That(stroke.HeightChangeAt(new Vector2(30f, 30f)), Is.EqualTo(0f));
            Assert.That(stroke.HeightChangeAt(new Vector2(-30f, 30f)), Is.Null);
            Assert.That(new TerrainFollower(session.World.Records, Space).Follow(stroke), Is.Empty);
        }

        [Test]
        public void ObjectsInAnotherSpace_DoNotFollow()
        {
            var elsewhere = RecordId.Parse("core:ref/elsewhere");
            var session = NewSession(At(OtherSpace, "core:ref/elsewhere", 20f, 10f, 20f));
            var stroke = RaiseStroke(session, dabs: 2);

            var followed = new TerrainFollower(session.World.Records, Space).Follow(stroke).Select(p => p.Id).ToList();

            Assert.That(followed, Has.Member(Resting));
            Assert.That(followed, Has.No.Member(elsewhere));

            TerrainEdits.Commit(session, stroke, new TerrainFollower(session.World.Records, Space));

            Assert.That(HeightOf(session, Resting), Is.EqualTo(11f).Within(1e-4f));
            Assert.That(HeightOf(session, elsewhere), Is.EqualTo(10f), "standing at the same spot in another space, it isn't on this land");
            Assert.That(session.World.Records.GetSource(elsewhere), Is.EqualTo("core"));
        }
    }
}
