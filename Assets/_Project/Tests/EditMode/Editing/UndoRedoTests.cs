using System;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class UndoRedoTests
    {
        static readonly RecordId Tree = RecordId.Parse("core:tree");
        static readonly RecordId Rock = RecordId.Parse("core:rock");

        static EditSession NewSession(int capacity = 256) =>
            new EditSession(
                new[] { Plugin("core", Base("core:tree", "Oak"), Base("core:rock", "Rock")) },
                Plugin("user", new[] { "core" }),
                capacity);

        static string TreeName(EditSession s) => s.World.Records.Get<BaseObjectRecord>(Tree).DisplayName;

        [Test]
        public void FreshSession_HasNothingToUndoOrRedo()
        {
            var session = NewSession();

            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.CanRedo, Is.False);
            Assert.That(session.Undo(), Is.False);
            Assert.That(session.Redo(), Is.False);
        }

        [Test]
        public void UndoRedo_NewRecord()
        {
            var session = NewSession();
            var id = session.NewId("ref");
            session.Put(Placed(id.ToString(), "core:tree"), "Place tree");

            Assert.That(session.UndoDescription, Is.EqualTo("Place tree"));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.World.Records.Contains(id), Is.False);
            Assert.That(session.RedoDescription, Is.EqualTo("Place tree"));

            Assert.That(session.Redo(), Is.True);
            Assert.That(session.World.Records.Contains(id), Is.True);
        }

        [Test]
        public void Undo_Override_RestoresMasterVersion()
        {
            var session = NewSession();
            session.Put(Base("core:tree", "Giant Oak"));

            session.Undo();

            Assert.That(TreeName(session), Is.EqualTo("Oak"));
            Assert.That(session.World.Records.GetSource(Tree), Is.EqualTo("core"));
            Assert.That(session.BuildActivePlugin().Records, Is.Empty);
        }

        [Test]
        public void Undo_DeleteOfOverriddenRecord_RestoresTheOverride()
        {
            var session = NewSession();
            session.Put(Base("core:tree", "Giant Oak"));
            session.Delete(Tree);

            session.Undo();

            Assert.That(TreeName(session), Is.EqualTo("Giant Oak"));
            Assert.That(session.BuildActivePlugin().Deletions, Is.Empty);
        }

        [Test]
        public void MultipleSteps_UndoInReverseOrder()
        {
            var session = NewSession();
            session.Put(Base("core:tree", "A"));
            session.Put(Base("core:tree", "B"));
            session.Put(Base("core:tree", "C"));

            session.Undo();
            Assert.That(TreeName(session), Is.EqualTo("B"));
            session.Undo();
            Assert.That(TreeName(session), Is.EqualTo("A"));
            session.Redo();
            Assert.That(TreeName(session), Is.EqualTo("B"));
        }

        [Test]
        public void NewEdit_ClearsRedo()
        {
            var session = NewSession();
            session.Put(Base("core:tree", "A"));
            session.Undo();

            session.Put(Base("core:tree", "B"));

            Assert.That(session.CanRedo, Is.False);
        }

        [Test]
        public void Batch_IsOneUndoStep()
        {
            var session = NewSession();

            session.Batch("Rename both", () =>
            {
                session.Put(Base("core:tree", "Pine"));
                session.Put(Base("core:rock", "Stone"));
            });

            Assert.That(session.UndoDescription, Is.EqualTo("Rename both"));
            session.Undo();
            Assert.That(TreeName(session), Is.EqualTo("Oak"));
            Assert.That(session.World.Records.Get<BaseObjectRecord>(Rock).DisplayName, Is.EqualTo("Rock"));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void Batch_ResolvesAndNotifiesOnce()
        {
            var session = NewSession();
            var notifications = 0;
            session.WorldChanged += _ => notifications++;

            session.Batch("Two edits", () =>
            {
                session.Put(Base("core:tree", "Pine"));
                session.Delete(Rock);
            });

            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void FailedBatch_RollsBackEverythingAndRecordsNothing()
        {
            var session = NewSession();

            Assert.Throws<DataValidationException>(() => session.Batch("Bad", () =>
            {
                session.Put(Base("core:tree", "Pine"));
                session.Put(Base("other:thing"));
            }));

            Assert.That(TreeName(session), Is.EqualTo("Oak"));
            Assert.That(session.BuildActivePlugin().Records, Is.Empty);
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void ExceptionInsideBatch_RollsBack()
        {
            var session = NewSession();

            Assert.Throws<InvalidOperationException>(() => session.Batch("Throws", () =>
            {
                session.Put(Base("core:tree", "Pine"));
                throw new InvalidOperationException("boom");
            }));

            Assert.That(TreeName(session), Is.EqualTo("Oak"));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void NoOpEdits_AreNotRecorded()
        {
            var session = NewSession();
            var id = session.NewId("ref");

            session.Batch("Place and remove", () =>
            {
                session.Put(Placed(id.ToString(), "core:tree"));
                session.Delete(id);
            });

            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void Dirty_TracksDistanceFromSavedState()
        {
            var session = NewSession();
            session.Put(Base("core:tree", "A"));
            session.MarkSaved();
            Assert.That(session.IsDirty, Is.False);

            session.Put(Base("core:tree", "B"));
            Assert.That(session.IsDirty, Is.True);

            session.Undo();
            Assert.That(session.IsDirty, Is.False, "Back at the saved state.");

            session.Undo();
            Assert.That(session.IsDirty, Is.True, "Before the saved state.");

            session.Redo();
            Assert.That(session.IsDirty, Is.False);
        }

        [Test]
        public void Capacity_DropsOldestSteps()
        {
            var session = NewSession(capacity: 2);
            session.Put(Base("core:tree", "A"));
            session.Put(Base("core:tree", "B"));
            session.Put(Base("core:tree", "C"));

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Undo(), Is.False);
            Assert.That(TreeName(session), Is.EqualTo("A"));
            Assert.That(session.IsDirty, Is.True, "State A was never saved.");
        }

        [Test]
        public void UndoRedo_RaiseWorldChanged()
        {
            var session = NewSession();
            session.Put(Base("core:tree", "A"));
            var notifications = 0;
            session.WorldChanged += _ => notifications++;

            session.Undo();
            session.Redo();

            Assert.That(notifications, Is.EqualTo(2));
        }
    }
}
