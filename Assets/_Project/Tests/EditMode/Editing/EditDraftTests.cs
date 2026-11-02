using System;
using System.Linq;
using System.Numerics;
using Forge.Data;
using Forge.Data.Plugins;
using Forge.Data.Records;
using Forge.Editing;
using NUnit.Framework;
using static Forge.Tests.Data.TestRecords;

namespace Forge.Tests.Editing
{
    public class EditDraftTests
    {
        static readonly RecordId Rock = RecordId.Parse("core:rock");

        static EditSession NewSession() =>
            new EditSession(
                new[] { Plugin("core", Base("core:tree", "Oak"), Base("core:rock", "Rock")) },
                Plugin("user", new[] { "core" }));

        static PlacedObjectRecord PutTree(EditSession session, float x = 0f)
        {
            var tree = Placed(session.NewId("ref").ToString(), "core:tree", x);
            session.Put(tree);
            return tree;
        }

        static int PlacedCount(EditSession session) => session.World.Records.OfType<PlacedObjectRecord>().Count();

        [Test]
        public void DraftEdits_ShowInTheWorld_ButStayOutOfTheHistory()
        {
            var session = NewSession();
            ResolvedWorld raised = null;
            session.WorldChanged += world => raised = world;

            var draft = session.BeginDraft();
            var tree = PutTree(session);

            Assert.That(session.World.Records.Contains(tree.Id), Is.True);
            Assert.That(raised, Is.SameAs(session.World), "Each draft edit publishes the world, so the scene shows it.");
            Assert.That(session.CanUndo, Is.False);
            Assert.That(draft.HasChanges, Is.True);
            Assert.That(draft.Revision, Is.EqualTo(1));
            Assert.That(session.IsDirty, Is.True);
        }

        [Test]
        public void Accept_RecordsTheWholeDraftAsOneUndoStep()
        {
            var session = NewSession();
            var draft = session.BeginDraft();
            PutTree(session);
            PutTree(session, 5f);

            draft.Accept("Agent: two trees");

            Assert.That(session.Draft, Is.Null);
            Assert.That(draft.IsOpen, Is.False);
            Assert.That(session.UndoDescription, Is.EqualTo("Agent: two trees"));

            session.Undo();
            Assert.That(PlacedCount(session), Is.Zero);
            Assert.That(session.CanUndo, Is.False, "Both trees were one step.");

            session.Redo();
            Assert.That(PlacedCount(session), Is.EqualTo(2));
        }

        [Test]
        public void Discard_RollsEverythingBack_WithoutHistory()
        {
            var session = NewSession();
            var draft = session.BeginDraft();
            PutTree(session);
            session.Delete(Rock);
            var raised = 0;
            session.WorldChanged += _ => raised++;

            draft.Discard();

            Assert.That(session.Draft, Is.Null);
            Assert.That(PlacedCount(session), Is.Zero);
            Assert.That(session.World.Records.Contains(Rock), Is.True);
            Assert.That(session.BuildActivePlugin().Records, Is.Empty);
            Assert.That(session.BuildActivePlugin().Deletions, Is.Empty);
            Assert.That(session.CanUndo || session.CanRedo, Is.False);
            Assert.That(session.IsDirty, Is.False);
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void UndoAfterAccept_ReturnsToTheStateBeforeTheDraft()
        {
            var session = NewSession();
            var first = PutTree(session);
            var draft = session.BeginDraft();
            session.Put(first.WithPosition(new Vector3(9f, 0f, 0f)));
            session.Put(first.WithPosition(new Vector3(12f, 0f, 0f)));
            PutTree(session, 3f);
            draft.Accept("Agent: rearrange");

            session.Undo();

            Assert.That(session.World.Records.Get<PlacedObjectRecord>(first.Id).Position, Is.EqualTo(first.Position));
            Assert.That(PlacedCount(session), Is.EqualTo(1));
            Assert.That(session.UndoDescription, Does.StartWith("Edit"), "The edit before the draft is still its own step.");
        }

        [Test]
        public void HistoryAndSaving_WaitWhileADraftIsOpen()
        {
            var session = NewSession();
            PutTree(session);
            session.Undo();
            session.BeginDraft();

            Assert.Throws<InvalidOperationException>(() => session.Undo());
            Assert.Throws<InvalidOperationException>(() => session.Redo());
            Assert.Throws<InvalidOperationException>(() => session.MarkSaved());
        }

        [Test]
        public void OnlyOneDraftAtATime_AndNeverInsideABatch()
        {
            var session = NewSession();

            Assert.Throws<InvalidOperationException>(() => session.Batch("Edit", () => session.BeginDraft()));
            Assert.That(session.Draft, Is.Null);

            session.BeginDraft();
            Assert.Throws<InvalidOperationException>(() => session.BeginDraft());
        }

        [Test]
        public void ARejectedEdit_RollsBackOnlyItself()
        {
            var session = NewSession();
            var draft = session.BeginDraft();
            var tree = PutTree(session);

            Assert.Throws<DataValidationException>(() => session.Put(Base("other:thing")));

            Assert.That(session.World.Records.Contains(tree.Id), Is.True);
            Assert.That(draft.Changes.Select(c => c.Id), Is.EqualTo(new[] { tree.Id }));
        }

        [Test]
        public void ChangesThatCancelOut_LeaveNothingToAccept()
        {
            var session = NewSession();
            var draft = session.BeginDraft();
            var tree = PutTree(session);
            session.Delete(tree.Id);

            Assert.That(draft.HasChanges, Is.False);
            Assert.That(session.IsDirty, Is.False);

            draft.Accept("Agent: nothing");
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void AClosedDraft_CannotBeClosedAgain()
        {
            var session = NewSession();
            var draft = session.BeginDraft();
            draft.Discard();

            Assert.Throws<InvalidOperationException>(() => draft.Accept("Again"));
            Assert.Throws<InvalidOperationException>(() => draft.Discard());
        }
    }
}
