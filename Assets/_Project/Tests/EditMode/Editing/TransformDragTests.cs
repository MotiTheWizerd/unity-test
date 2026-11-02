using System;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Geometry;
using Forge.Editing.Transforms;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class TransformDragTests
    {
        const float Tolerance = 1e-4f;
        static readonly Vector3 Down = -Vector3.UnitY;

        // A ray straight down onto the ground plane at (x, z).
        static Vector3 Above(float x, float z) => new Vector3(x, 10f, z);

        static PlacedObjectRecord At(float x, float y = 0f, float z = 0f, float scale = 1f) =>
            new PlacedObjectRecord(RecordId.Parse("core:ref/1"), RecordId.Parse("core:tree"), new Vector3(x, y, z), Quaternion.Identity, scale);

        static void AssertNear(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), $"expected {expected} but was {actual}");

        [Test]
        public void MoveAlongAxis_FollowsPointerAlongTheAxisOnly()
        {
            var drag = MoveDrag.TryBegin(DragConstraint.Line(Vector3.Zero, Vector3.UnitX), Above(1, 0), Down);

            drag.Update(Above(4, 0), Down, snap: false);

            AssertNear(drag.Delta, new Vector3(3, 0, 0));
            AssertNear(drag.Apply(At(1)).Position, new Vector3(4, 0, 0));
            AssertNear(drag.HandlePosition, new Vector3(3, 0, 0));
        }

        [Test]
        public void MoveOnPlane_IgnoresMotionOffThePlane_AndSnapsToHalfMeters()
        {
            var drag = MoveDrag.TryBegin(DragConstraint.Plane(Vector3.Zero, Vector3.UnitY), Above(0, 0), Down);

            drag.Update(Above(2.2f, 4.9f), Down, snap: true);

            AssertNear(drag.Delta, new Vector3(2, 0, 5));
        }

        [Test]
        public void Rotate_QuarterTurnAroundY_TurnsPositionsAndOrientationsTogether()
        {
            var drag = RotateDrag.TryBegin(Vector3.Zero, Vector3.UnitY, Above(1, 0), Down);

            drag.Update(Above(0, -1), Down, snap: false);
            var rotated = drag.Apply(At(1));

            Assert.That(Math.Abs(drag.Degrees), Is.EqualTo(90f).Within(Tolerance));
            AssertNear(rotated.Position, new Vector3(0, 0, -1));
            // The object's own +X axis now points where its position went.
            AssertNear(Vector3.Transform(Vector3.UnitX, rotated.Rotation), new Vector3(0, 0, -1));
        }

        [Test]
        public void Rotate_AppliesAfterTheObjectsOwnRotation()
        {
            var turned = At(0).WithRotation(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI / 2));
            var drag = RotateDrag.TryBegin(Vector3.Zero, Vector3.UnitY, Above(1, 0), Down);
            drag.Update(Above(0, -1), Down, snap: false);

            var result = drag.Apply(turned);

            var local = new Vector3(0.3f, 0.5f, 0.7f);
            AssertNear(Vector3.Transform(local, result.Rotation), Vector3.Transform(Vector3.Transform(local, turned.Rotation), drag.Rotation));
        }

        [Test]
        public void Rotate_AccumulatesPastHalfATurn_AndSnapsTo15Degrees()
        {
            var drag = RotateDrag.TryBegin(Vector3.Zero, Vector3.UnitY, Above(1, 0), Down);

            // Walk the pointer around the ring in 10° steps, 270° in total.
            for (var degrees = 10; degrees <= 270; degrees += 10)
            {
                var radians = degrees * Math.PI / 180;
                drag.Update(Above((float)Math.Cos(radians), (float)-Math.Sin(radians)), Down, snap: false);
            }

            Assert.That(Math.Abs(drag.Degrees), Is.EqualTo(270f).Within(1e-2f));

            drag.Update(Above((float)Math.Cos(4.0), (float)-Math.Sin(4.0)), Down, snap: true);
            Assert.That(Math.Abs(drag.Degrees) % RotateDrag.SnapDegrees, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ScaleAlongAxis_IsRatioOfDistances_AndSpreadsPositionsFromPivot()
        {
            var drag = ScaleDrag.TryBegin(DragConstraint.Line(Vector3.Zero, Vector3.UnitX), Above(2, 0), Down);

            drag.Update(Above(3, 0), Down, snap: false);
            var scaled = drag.Apply(At(2, scale: 2f));

            Assert.That(drag.Factor, Is.EqualTo(1.5f).Within(Tolerance));
            AssertNear(scaled.Position, new Vector3(3, 0, 0));
            Assert.That(scaled.Scale, Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void Scale_ThroughThePivot_ClampsToMinimum_AndSnaps()
        {
            var drag = ScaleDrag.TryBegin(DragConstraint.Line(Vector3.Zero, Vector3.UnitX), Above(2, 0), Down);

            drag.Update(Above(-1, 0), Down, snap: false);
            Assert.That(drag.Factor, Is.EqualTo(ScaleDrag.MinFactor));

            drag.Update(Above(2.74f, 0), Down, snap: true);
            Assert.That(drag.Factor, Is.EqualTo(1.4f).Within(Tolerance));
        }

        [Test]
        public void ScaleOnPlane_UsesRadialDistance()
        {
            var drag = ScaleDrag.TryBegin(DragConstraint.Plane(Vector3.Zero, Vector3.UnitY), Above(0, 1), Down);

            drag.Update(Above(-2, 0), Down, snap: false);

            Assert.That(drag.Factor, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void Drags_CannotStartOnThePivotOrOffTheirConstraint()
        {
            Assert.That(ScaleDrag.TryBegin(DragConstraint.Line(Vector3.Zero, Vector3.UnitX), Above(0, 0), Down), Is.Null);
            Assert.That(RotateDrag.TryBegin(Vector3.Zero, Vector3.UnitY, Above(0, 0), Down), Is.Null);
            Assert.That(MoveDrag.TryBegin(DragConstraint.Plane(Vector3.Zero, Vector3.UnitY), Above(0, 0), Vector3.UnitY), Is.Null);
        }

        [Test]
        public void FitDistance_FitsTheSphereInTheNarrowerFieldOfView()
        {
            // 90° vertical FOV on a square view: the sphere touches the frustum at 45° from the axis.
            Assert.That(ViewMath.FitDistance(1f, 90f, 1f), Is.EqualTo((float)Math.Sqrt(2)).Within(Tolerance));
            // A tall, narrow view is limited horizontally, so it needs more distance.
            Assert.That(ViewMath.FitDistance(1f, 90f, 0.5f), Is.GreaterThan(ViewMath.FitDistance(1f, 90f, 2f)));
            Assert.That(ViewMath.FitDistance(0f, 60f, 1.5f), Is.EqualTo(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ViewMath.FitDistance(1f, 0f, 1f));
        }

        [Test]
        public void SignedAngle_MatchesCreateFromAxisAngle()
        {
            var from = Vector3.UnitX;
            var to = Vector3.Normalize(new Vector3(1, 0, -1));

            var angle = DragMath.SignedAngle(from, to, Vector3.UnitY);

            AssertNear(Vector3.Transform(from, Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle)), to);
        }
    }

    public class ObjectTransformTests
    {
        static readonly RecordId Ref1 = RecordId.Parse("core:ref/1");
        static readonly RecordId Ref2 = RecordId.Parse("core:ref/2");

        static EditSession NewSession() =>
            new EditSession(
                new[] { Plugin("core", Base("core:tree"), Placed("core:ref/1", "core:tree", x: 1f), Placed("core:ref/2", "core:tree", x: 3f)) },
                Plugin("user", new[] { "core" }));

        [Test]
        public void Transform_IsOneUndoStep_AndSkipsUnchangedObjects()
        {
            var session = NewSession();

            var changed = ObjectEdits.Transform(session, new[] { Ref1, Ref2 }, p => p.Id == Ref1 ? p.WithScale(2f) : p, "Scale");

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(session.World.Records.Get<PlacedObjectRecord>(Ref1).Scale, Is.EqualTo(2f));
            Assert.That(session.World.Records.GetSource(Ref2), Is.EqualTo("core"), "unchanged objects are not overridden");
            Assert.That(session.UndoDescription, Is.EqualTo("Scale ref/1"));

            session.Undo();
            Assert.That(session.World.Records.Get<PlacedObjectRecord>(Ref1).Scale, Is.EqualTo(1f));
        }

        [Test]
        public void Transform_RejectsChangesThatReplaceTheRecord()
        {
            var session = NewSession();

            Assert.Throws<ArgumentException>(() =>
                ObjectEdits.Transform(session, new[] { Ref1 }, p => Placed("core:ref/2", "core:tree", x: 9f), "Bad"));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void RotateDrag_CommitsThroughTransform_ForAWholeSelection()
        {
            var session = NewSession();
            var drag = RotateDrag.TryBegin(new Vector3(2, 0, 0), Vector3.UnitY, new Vector3(3, 10, 0), -Vector3.UnitY);
            drag.Update(new Vector3(2, 10, -1), -Vector3.UnitY, snap: false);

            ObjectEdits.Transform(session, new[] { Ref1, Ref2 }, drag.Apply, drag.Verb);

            // Two objects 1 m either side of the pivot swap sides of the Z axis after a quarter turn.
            var a = session.World.Records.Get<PlacedObjectRecord>(Ref1).Position;
            var b = session.World.Records.Get<PlacedObjectRecord>(Ref2).Position;
            Assert.That(Vector3.Distance(a, new Vector3(2, 0, 1)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(b, new Vector3(2, 0, -1)), Is.LessThan(1e-4f));
            Assert.That(session.UndoDescription, Is.EqualTo("Rotate 2 objects"));
        }
    }
}
