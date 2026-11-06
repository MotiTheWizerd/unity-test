using System;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Records;
using Forge.Editing;
using Forge.Editing.Geometry;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class SelectionTests
    {
        static readonly RecordId A = RecordId.Parse("core:a");
        static readonly RecordId B = RecordId.Parse("core:b");

        [Test]
        public void SetToggleClear_MaintainOrderAndRaiseChanged()
        {
            var selection = new Selection();
            var changes = 0;
            selection.Changed += () => changes++;

            selection.Set(A);
            selection.Toggle(B);
            Assert.That(selection.Ids, Is.EqualTo(new[] { A, B }));

            selection.Toggle(A);
            Assert.That(selection.Ids, Is.EqualTo(new[] { B }));

            selection.Clear();
            Assert.That(selection.IsEmpty, Is.True);
            Assert.That(changes, Is.EqualTo(4));
        }

        [Test]
        public void RedundantChanges_DoNotRaiseChanged()
        {
            var selection = new Selection();
            selection.Set(A);
            var changes = 0;
            selection.Changed += () => changes++;

            selection.Set(A);
            selection.Replace(new[] { A, A });
            new Selection().Clear();

            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void DefaultIds_AreIgnored()
        {
            var selection = new Selection();

            selection.Toggle(default);
            selection.Replace(new[] { default(RecordId), A });

            Assert.That(selection.Ids, Is.EqualTo(new[] { A }));
        }

        [Test]
        public void Prune_DropsIdsMissingFromTheWorld()
        {
            var session = new EditSession(new[] { Plugin("core", Base("core:a")) }, Plugin("user", new[] { "core" }));
            var selection = new Selection();
            selection.Replace(new[] { A, B });

            selection.Prune(session.World.Records);

            Assert.That(selection.Ids, Is.EqualTo(new[] { A }));
        }
    }

    public class ObjectEditsTests
    {
        static EditSession NewSession() =>
            new EditSession(
                new[]
                {
                    Plugin("core",
                        Base("core:tree"),
                        new MaterialRecord(Oak, "Oak", "material/oak"),
                        Placed("core:ref/1", "core:tree", x: 1f),
                        Placed("core:ref/2", "core:tree", x: 3f)),
                },
                Plugin("user", new[] { "core" }));

        static readonly RecordId Ref1 = RecordId.Parse("core:ref/1");
        static readonly RecordId Ref2 = RecordId.Parse("core:ref/2");
        static readonly RecordId Tree = RecordId.Parse("core:tree");
        static readonly RecordId Oak = MaterialRecord.IdFor("core", "oak");

        static RecordId? MaterialOf(EditSession s, RecordId id, string slot) => s.World.Records.Get<PlacedObjectRecord>(id).MaterialIn(slot);

        [Test]
        public void SetMaterial_DressesASlotOfEachObject_InOneUndoStep_AsAnOverride()
        {
            var session = NewSession();

            var changed = ObjectEdits.SetMaterial(session, new[] { Ref1, Ref2, Tree }, "Bark", Oak);

            Assert.That(changed, Is.EqualTo(2));
            Assert.That(MaterialOf(session, Ref1, "Bark"), Is.EqualTo(Oak));
            Assert.That(MaterialOf(session, Ref2, "Bark"), Is.EqualTo(Oak));
            Assert.That(session.World.Records.GetSource(Ref1), Is.EqualTo("user"));
            Assert.That(session.UndoDescription, Is.EqualTo("Set material of 2 objects"));
            Assert.That(ObjectEdits.SetMaterial(session, new[] { Ref1, Ref2 }, "Bark", Oak), Is.EqualTo(0), "Already wearing it.");

            session.Undo();
            Assert.That(MaterialOf(session, Ref1, "Bark"), Is.Null);
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void SetMaterial_Null_AndResetMaterials_GiveBackTheBaseLook()
        {
            var session = NewSession();
            ObjectEdits.SetMaterial(session, new[] { Ref1 }, "Bark", Oak);
            ObjectEdits.SetMaterial(session, new[] { Ref1 }, "Leaves", Oak);

            Assert.That(ObjectEdits.SetMaterial(session, new[] { Ref1 }, "Bark", null), Is.EqualTo(1));
            Assert.That(session.UndoDescription, Is.EqualTo("Reset material of ref/1"));
            Assert.That(MaterialOf(session, Ref1, "Leaves"), Is.EqualTo(Oak));

            Assert.That(ObjectEdits.ResetMaterials(session, new[] { Ref1, Ref2 }), Is.EqualTo(1), "Ref2 wears its base look already.");
            Assert.That(session.World.Records.Get<PlacedObjectRecord>(Ref1).Materials, Is.Empty);
        }

        [Test]
        public void SetMaterial_RefusesWhatIsNotAMaterial()
        {
            var session = NewSession();

            Assert.Throws<ArgumentException>(() => ObjectEdits.SetMaterial(session, new[] { Ref1 }, "Bark", Tree));
            Assert.Throws<ArgumentException>(() => ObjectEdits.SetMaterial(session, new[] { Ref1 }, "Bark", MaterialRecord.IdFor("core", "lava")));
            Assert.Throws<ArgumentException>(() => ObjectEdits.SetMaterial(session, new[] { Ref1 }, " ", Oak));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void MakeVariant_SavesTheLook_AsANewKind_TheObjectNowIsOne_InOneUndoStep()
        {
            var session = NewSession();
            ObjectEdits.SetMaterial(session, new[] { Ref1 }, "Bark", Oak);

            var variant = ObjectEdits.MakeVariant(session, Ref1, "  Oak-bark Tree ");

            var kind = session.World.Records.Get<BaseObjectRecord>(variant);
            var tree = session.World.Records.Get<BaseObjectRecord>(Tree);
            var placed = session.World.Records.Get<PlacedObjectRecord>(Ref1);
            Assert.That(variant.ToString(), Is.EqualTo("user:variant/000001"));
            Assert.That((kind.DisplayName, kind.AssetKey, kind.Category), Is.EqualTo(("Oak-bark Tree", tree.AssetKey, tree.Category)));
            Assert.That(kind.Materials, Is.EqualTo(new[] { new MaterialOverride("Bark", Oak) }));
            Assert.That(placed.BaseId, Is.EqualTo(variant));
            Assert.That(placed.Materials, Is.Empty, "Its look now comes from the variant.");
            Assert.That(session.UndoDescription, Is.EqualTo("Make variant Oak-bark Tree"));

            session.Undo();
            Assert.That(session.World.Records.Contains(variant), Is.False);
            Assert.That(session.World.Records.Get<PlacedObjectRecord>(Ref1).MaterialIn("Bark"), Is.EqualTo(Oak));
        }

        [Test]
        public void MakeVariant_OfAnItem_IsAnItem()
        {
            var potion = new BaseObjectRecord(RecordId.Parse("core:potion"), "Potion", "items/potion", item: new ItemPart(ItemKind.Potion, 0.5f, 15));
            var session = new EditSession(new[] { Plugin("core", potion, Placed("core:ref/p", "core:potion")) }, Plugin("user", new[] { "core" }));

            var variant = ObjectEdits.MakeVariant(session, RecordId.Parse("core:ref/p"), "Blue Potion");

            Assert.That(session.World.Records.Get<BaseObjectRecord>(variant).Item, Is.SameAs(potion.Item));
        }

        [Test]
        public void MakeVariant_RefusesABlankName_AndWhatIsNotAPlacedObject()
        {
            var session = NewSession();

            Assert.Throws<ArgumentException>(() => ObjectEdits.MakeVariant(session, Ref1, " "));
            Assert.Throws<ArgumentException>(() => ObjectEdits.MakeVariant(session, Tree, "Tree 2"));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void MaterialOverrides_Layer_PutsAnObjectsOwnOverItsKinds()
        {
            var stone = MaterialRecord.IdFor("core", "stone");
            var under = new[] { new MaterialOverride("Bark", Oak), new MaterialOverride("Leaves", Oak) };
            var over = new[] { new MaterialOverride("Leaves", stone) };

            Assert.That(MaterialOverrides.Layer(under, over), Is.EqualTo(new[] { new MaterialOverride("Bark", Oak), new MaterialOverride("Leaves", stone) }));
            Assert.That(MaterialOverrides.Layer(null, over), Is.SameAs(over));
            Assert.That(MaterialOverrides.Layer(under, MaterialOverrides.None), Is.SameAs(under));
        }

        [Test]
        public void Transform_KeepsAnObjectsMaterials()
        {
            var session = NewSession();
            ObjectEdits.SetMaterial(session, new[] { Ref1 }, "Bark", Oak);

            ObjectEdits.Move(session, new[] { Ref1 }, Vector3.UnitY);

            Assert.That(MaterialOf(session, Ref1, "Bark"), Is.EqualTo(Oak));
        }

        static Vector3 PositionOf(EditSession s, RecordId id) => s.World.Records.Get<PlacedObjectRecord>(id).Position;

        [Test]
        public void Move_ShiftsAllPlacedObjectsInOneUndoStep()
        {
            var session = NewSession();

            var moved = ObjectEdits.Move(session, new[] { Ref1, Ref2, Tree }, new Vector3(0, 2, 5));

            Assert.That(moved, Is.EqualTo(2), "The base object is not a placed object and is skipped.");
            Assert.That(PositionOf(session, Ref1), Is.EqualTo(new Vector3(1, 2, 5)));
            Assert.That(PositionOf(session, Ref2), Is.EqualTo(new Vector3(3, 2, 5)));
            Assert.That(session.UndoDescription, Is.EqualTo("Move 2 objects"));

            session.Undo();
            Assert.That(PositionOf(session, Ref1), Is.EqualTo(new Vector3(1, 0, 0)));
            Assert.That(PositionOf(session, Ref2), Is.EqualTo(new Vector3(3, 0, 0)));
        }

        [Test]
        public void Move_MasterObject_BecomesOverride()
        {
            var session = NewSession();

            ObjectEdits.Move(session, new[] { Ref1 }, Vector3.UnitY);

            Assert.That(session.World.Records.GetSource(Ref1), Is.EqualTo("user"));
            Assert.That(session.UndoDescription, Is.EqualTo("Move ref/1"));
        }

        [Test]
        public void Move_ZeroDeltaOrNothingSelected_DoesNothing()
        {
            var session = NewSession();

            Assert.That(ObjectEdits.Move(session, new[] { Ref1 }, Vector3.Zero), Is.EqualTo(0));
            Assert.That(ObjectEdits.Move(session, new RecordId[0], Vector3.UnitX), Is.EqualTo(0));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void SetPositions_IsOneUndoStepAndSkipsUnchanged()
        {
            var session = NewSession();
            var positions = new System.Collections.Generic.Dictionary<RecordId, Vector3>
            {
                { Ref1, new Vector3(1, 7, 0) },
                { Ref2, new Vector3(3, 0, 0) }, // already there
                { Tree, new Vector3(9, 9, 9) }, // not a placed object
            };

            var moved = ObjectEdits.SetPositions(session, positions, "Drop");

            Assert.That(moved, Is.EqualTo(1));
            Assert.That(PositionOf(session, Ref1), Is.EqualTo(new Vector3(1, 7, 0)));
            Assert.That(session.UndoDescription, Is.EqualTo("Drop ref/1"));
            session.Undo();
            Assert.That(PositionOf(session, Ref1), Is.EqualTo(new Vector3(1, 0, 0)));
        }

        [Test]
        public void SetPositions_NothingToChange_RecordsNothing()
        {
            var session = NewSession();
            var positions = new System.Collections.Generic.Dictionary<RecordId, Vector3> { { Ref1, new Vector3(1, 0, 0) } };

            Assert.That(ObjectEdits.SetPositions(session, positions, "Drop"), Is.EqualTo(0));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void Delete_RemovesAllInOneUndoStep()
        {
            var session = NewSession();

            var deleted = ObjectEdits.Delete(session, new[] { Ref1, Ref2 });

            Assert.That(deleted, Is.EqualTo(2));
            Assert.That(session.World.Records.Contains(Ref1), Is.False);
            session.Undo();
            Assert.That(session.World.Records.Contains(Ref1), Is.True);
            Assert.That(session.World.Records.Contains(Ref2), Is.True);
        }

        [Test]
        public void Duplicate_CopiesWhereTheyStand_WithMaterials_AsOneUndoStep()
        {
            var session = NewSession();
            ObjectEdits.SetMaterial(session, new[] { Ref1 }, "Bark", Oak);
            var original = session.World.Records.Get<PlacedObjectRecord>(Ref1);

            var copies = ObjectEdits.Duplicate(session, new[] { Ref1, Tree, Ref1 }, "Duplicate");

            Assert.That(copies, Has.Count.EqualTo(1), "The base object isn't placed, and an id twice is copied once.");
            var copy = session.World.Records.Get<PlacedObjectRecord>(copies[0].Id);
            Assert.That(copy.Id, Is.Not.EqualTo(Ref1));
            Assert.That(copy.BaseId, Is.EqualTo(original.BaseId));
            Assert.That(copy.Space, Is.EqualTo(original.Space));
            Assert.That(copy.HasSameTransform(original), Is.True);
            Assert.That(copy.MaterialIn("Bark"), Is.EqualTo(Oak));
            Assert.That(session.UndoDescription, Does.StartWith("Duplicate"));

            session.Undo();
            Assert.That(session.World.Records.Contains(copy.Id), Is.False);
            Assert.That(session.World.Records.Contains(Ref1), Is.True);
        }

        [Test]
        public void Duplicate_SetsTheCopiesDownWhereTheCallerSays()
        {
            var session = NewSession();

            var copies = ObjectEdits.Duplicate(session, new[] { Ref1, Ref2 }, "Duplicate", copy => copy.WithPosition(copy.Position + new Vector3(10, 0, 0)));

            Assert.That(copies.Select(c => session.World.Records.Get<PlacedObjectRecord>(c.Id).Position),
                Is.EqualTo(new[] { new Vector3(11, 0, 0), new Vector3(13, 0, 0) }));
            Assert.That(PositionOf(session, Ref1), Is.EqualTo(new Vector3(1, 0, 0)), "The originals stay.");
        }

        [Test]
        public void Duplicate_NothingPlaced_RecordsNothing()
        {
            var session = NewSession();

            Assert.That(ObjectEdits.Duplicate(session, new[] { Tree }, "Duplicate"), Is.Empty);
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void Pivot_IsAveragePosition()
        {
            var session = NewSession();

            Assert.That(ObjectEdits.Pivot(session, new[] { Ref1, Ref2 }), Is.EqualTo(new Vector3(2, 0, 0)));
            Assert.That(ObjectEdits.Pivot(session, new[] { Tree }), Is.Null);
        }

        [Test]
        public void PlacedObject_WithMethods_KeepOtherFields()
        {
            var original = Placed("core:ref/1", "core:tree", x: 1f);

            var moved = original.WithPosition(new Vector3(9, 9, 9)).WithScale(2f);

            Assert.That(moved.Id, Is.EqualTo(original.Id));
            Assert.That(moved.BaseId, Is.EqualTo(original.BaseId));
            Assert.That(moved.Rotation, Is.EqualTo(original.Rotation));
            Assert.That(moved.Position, Is.EqualTo(new Vector3(9, 9, 9)));
            Assert.That(moved.Scale, Is.EqualTo(2f));
        }
    }

    public class DragMathTests
    {
        [Test]
        public void ProjectOnAxis_FindsClosestPointAlongAxis()
        {
            // Camera looking straight down at x = 5 onto the X axis.
            var ok = DragMath.TryProjectOnAxis(new Vector3(5, 10, 0), -Vector3.UnitY, Vector3.Zero, Vector3.UnitX, out var t);

            Assert.That(ok, Is.True);
            Assert.That(t, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void ProjectOnAxis_WorksForSkewedRays()
        {
            // Ray passes above the Y axis at height 3, looking along +X.
            var ok = DragMath.TryProjectOnAxis(new Vector3(-10, 3, 0), Vector3.UnitX, Vector3.Zero, Vector3.UnitY, out var t);

            Assert.That(ok, Is.True);
            Assert.That(t, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void ProjectOnAxis_ParallelRay_Fails()
        {
            Assert.That(DragMath.TryProjectOnAxis(Vector3.UnitY, Vector3.UnitX, Vector3.Zero, Vector3.UnitX, out _), Is.False);
        }

        [Test]
        public void IntersectPlane_HitsGroundPlane()
        {
            var ok = DragMath.TryIntersectPlane(new Vector3(0, 10, 0), Vector3.Normalize(new Vector3(1, -1, 0)), Vector3.Zero, Vector3.UnitY, out var hit);

            Assert.That(ok, Is.True);
            Assert.That(hit.X, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(hit.Y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void IntersectPlane_ParallelOrBehind_Fails()
        {
            Assert.That(DragMath.TryIntersectPlane(new Vector3(0, 10, 0), Vector3.UnitX, Vector3.Zero, Vector3.UnitY, out _), Is.False);
            Assert.That(DragMath.TryIntersectPlane(new Vector3(0, 10, 0), Vector3.UnitY, Vector3.Zero, Vector3.UnitY, out _), Is.False);
        }
    }
}
